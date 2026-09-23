using System.Collections;
using System.Reflection;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Tests.Gui;

internal static class ControlTreeHelper
{
    // Finds every T under host. Child controls reach a host either through its Controls tree or
    // held privately in fields (single controls, lists of tabs or per-core charts, per-key caches),
    // so both are searched.
    internal static List<T> FindAll<T>(Control host) where T : Control
    {
        HashSet<T> found = new(ReferenceEqualityComparer.Instance);
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);

        Visit(host);

        return [.. found];

        void Visit(Control control)
        {
            if (!visited.Add(control)) {
                return;
            }

            if (control is T match) {
                found.Add(match);
            }

            foreach (Control child in control.Controls) {
                Visit(child);
            }

            for (Type? type = control.GetType(); type is not null && type != typeof(Control); type = type.BaseType) {
                foreach (FieldInfo field in type.GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
                    foreach (Control held in ControlsIn(field.GetValue(control))) {
                        Visit(held);
                    }
                }
            }
        }
    }

    private static IEnumerable<Control> ControlsIn(object? value) => value switch {
        Control control => [control],
        IDictionary dictionary => dictionary.Values.OfType<Control>(),
        IEnumerable sequence and not string => sequence.OfType<Control>(),
        _ => []
    };
}
