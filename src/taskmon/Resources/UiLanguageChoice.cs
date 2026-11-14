namespace Task.Monitor.Resources;

// A language Setup offers: the ui-language code it saves, and the language's name in that
// language, so its speakers can find it whatever language the screens are in now.
internal sealed record UiLanguageChoice(string Code, string NativeName);
