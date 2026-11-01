namespace Task.Monitor.System.Services.Health;

internal sealed class HealthTracker
{
    private readonly ConditionTimer belowCritical = new();
    private readonly ConditionTimer belowWarning  = new();
    private readonly ConditionTimer critical      = new();
    private readonly ConditionTimer warning       = new();

    private DateTime?   lastUpdateUtc;
    private HealthLevel peakLevel;

    private sealed class ConditionTimer
    {
        private double lapsedSeconds;

        public bool HasHeld(int seconds, DateTime utcNow) =>
            Since is { } start && (utcNow - start).TotalSeconds >= seconds;

        public void Reset() => Since = null;

        public DateTime? Since { get; private set; }

        public void Update(bool isHolding, double elapsedSeconds, int graceSeconds, DateTime utcNow)
        {
            if (isHolding) {
                if (Since == null) {
                    Since         = utcNow;
                    lapsedSeconds = 0.0;
                }

                return;
            }

            if (Since == null) {
                return;
            }

            lapsedSeconds += elapsedSeconds;

            if (lapsedSeconds > graceSeconds) {
                Since = null;
            }
        }
    }

    public bool IsUnderPressure(HealthReading reading, HealthThresholds thresholds, DateTime utcNow) =>
        Level >= HealthLevel.Warning &&
        (reading.Level >= HealthLevel.Warning || !belowWarning.HasHeld(thresholds.SustainGraceSeconds, utcNow));

    public HealthLevel Level { get; private set; } = HealthLevel.Unknown;

    public HealthEvent? OpenEvent { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    private void Reset()
    {
        belowCritical.Reset();
        belowWarning.Reset();
        critical.Reset();
        warning.Reset();
    }

    private HealthLevel Target(HealthReading reading, HealthThresholds thresholds, DateTime utcNow)
    {
        if (reading.IsImmediate) {
            return reading.Level;
        }

        if (reading.Level == HealthLevel.Critical && critical.HasHeld(thresholds.CriticalSustainSeconds, utcNow)) {
            return HealthLevel.Critical;
        }

        return reading.Level >= HealthLevel.Warning && warning.HasHeld(thresholds.WarningSustainSeconds, utcNow)
            ? HealthLevel.Warning
            : HealthLevel.Ok;
    }

    public HealthEvent? Update(
        HealthReading         reading,
        HealthThresholds      thresholds,
        DateTime              utcNow,
        IReadOnlyList<string> culprits)
    {
        if (reading.Level == HealthLevel.Unknown) {
            HealthEvent? closed = OpenEvent is { } open ? open with { EndUtc = utcNow } : null;

            Level     = HealthLevel.Unknown;
            OpenEvent = null;
            Reset();

            return closed;
        }

        double elapsedSeconds = lastUpdateUtc is { } last ? (utcNow - last).TotalSeconds : 0.0;

        lastUpdateUtc = utcNow;

        int graceSeconds = thresholds.SustainGraceSeconds;

        warning.Update(
            reading.Level >= HealthLevel.Warning, 
            elapsedSeconds, 
            graceSeconds, 
            utcNow);
        
        critical.Update(
            reading.Level == HealthLevel.Critical, 
            elapsedSeconds, 
            graceSeconds, 
            utcNow);
        
        belowWarning.Update(
            reading.Level < HealthLevel.Warning, 
            elapsedSeconds, 
            graceSeconds, 
            utcNow);
        
        belowCritical.Update(
            reading.Level < HealthLevel.Critical, 
            elapsedSeconds, 
            graceSeconds, 
            utcNow);

        if (reading.Level >= HealthLevel.Warning) {
            Reason = reading.Reason;
        }

        UpdateLevel(reading, thresholds, utcNow);

        if (Level >= HealthLevel.Warning) {
            UpdateOpenEvent(reading, utcNow, culprits);
            return null;
        }

        HealthEvent? finished = OpenEvent is { } episode ? episode with { EndUtc = utcNow } : null;
        OpenEvent = null;

        return finished;
    }

    private void UpdateLevel(HealthReading reading, HealthThresholds thresholds, DateTime utcNow)
    {
        HealthLevel target = Target(reading, thresholds, utcNow);

        if (target > Level) {
            Level = target;

            belowCritical.Reset();
            belowWarning.Reset();
            return;
        }

        if (Level == HealthLevel.Critical &&
            reading.Level < HealthLevel.Critical &&
            belowCritical.HasHeld(thresholds.ClearSeconds, utcNow)) {

            Level = HealthLevel.Warning;
        }

        if (Level == HealthLevel.Warning &&
            reading.Level < HealthLevel.Warning &&
            belowWarning.HasHeld(thresholds.ClearSeconds, utcNow)) {

            Level = HealthLevel.Ok;
        }
    }

    private void UpdateOpenEvent(HealthReading reading, DateTime utcNow, IReadOnlyList<string> culprits)
    {
        if (OpenEvent == null) {
            peakLevel = reading.Level;
            OpenEvent = new HealthEvent {
                Resource   = reading.Resource,
                Level      = Level,
                StartUtc   = warning.Since ?? utcNow,
                PeakReason = Reason,
                PeakValue  = reading.Severity,
                Culprits   = culprits
            };
        }

        if (Level > OpenEvent.Level) {
            OpenEvent = OpenEvent with { Level = Level };
        }

        if (reading.Level > peakLevel || (reading.Level == peakLevel && reading.Severity > OpenEvent.PeakValue)) {
            peakLevel = reading.Level;
            OpenEvent = OpenEvent with {
                PeakReason = reading.Reason,
                PeakValue  = reading.Severity,
                Culprits   = culprits
            };
        }
    }
}
