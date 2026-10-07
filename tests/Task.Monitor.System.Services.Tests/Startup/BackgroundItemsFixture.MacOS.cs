#if __APPLE__
namespace Task.Monitor.System.Services.Tests.Startup;

// A real NSKeyedArchiver archive (written by Foundation, not hand built) in the shape of the
// Background Task Management database: $top "store" -> Storage.itemsByUserIdentifier, mapping a
// user GeneratedUID to an array of ItemRecord objects. It holds:
//   52C7040A-60B5-44E4-8425-8F18E40FBF82 (a user)
//     Example        app (0x2), enabled+allowed, file URL "/Applications/Example App.app/", an NSUUID
//     Helper         login item (0x4), allowed only, bundle relative URL, parent 2.com.example.app
//     Based          agent (0x8), NSURL with an NS.base of the app's file URL
//     NoDisposition  app with no disposition field - not a valid record
//   FFFFEEEE-DDDD-CCCC-BBBB-AAAAFFFFFFFE (nobody, system wide)
//     com.example.daemon  legacy daemon (0x10010), allowed only
internal static class BackgroundItemsFixture
{
    public const string UserUuid = "52C7040A-60B5-44E4-8425-8F18E40FBF82";
    public const string SystemUuid = "FFFFEEEE-DDDD-CCCC-BBBB-AAAAFFFFFFFE";

    public static byte[] Bytes => Convert.FromBase64String(Archive);

    private const string Archive =
        "YnBsaXN0MDDUAQIDBAUGBwpYJHZlcnNpb25ZJGFyY2hpdmVyVCR0b3BYJG9iamVjdHMSAAGGoF8QD05TS2V5ZWRBcmNoaXZl" +
        "ctEICVVzdG9yZYABrxAtCwwRGxwdISsxMjg5Ojs+QUhWV1hZXWBhYmZncXJzdHV2enuBgoOHi4yNkZKVVSRudWxs0g0ODxBW" +
        "JGNsYXNzXxAVaXRlbXNCeVVzZXJJZGVudGlmaWVygCyAAtMSEw0UFxpXTlMua2V5c1pOUy5vYmplY3RzohUWgAOABKIYGYAF" +
        "gA+AK18QJEZGRkZFRUVFLUREREQtQ0NDQy1CQkJCLUFBQUFGRkZGRkZGRV8QJDUyQzcwNDBBLTYwQjUtNDRFNC04NDI1LThG" +
        "MThFNDBGQkY4MtITDR4goR+ABoAO1SINIyQlJicoKSpUdHlwZVN1cmxUbmFtZVtkaXNwb3NpdGlvboAMgA2AB4AKgAvTLA0t" +
        "Li8wV05TLmJhc2VbTlMucmVsYXRpdmWAAIAJgAhfEDZmaWxlOi8vL0xpYnJhcnkvTGF1bmNoRGFlbW9ucy9jb20uZXhhbXBs" +
        "ZS5kYWVtb24ucGxpc3TSMzQ1NlokY2xhc3NuYW1lWCRjbGFzc2VzVU5TVVJMojU3WE5TT2JqZWN0XxASY29tLmV4YW1wbGUu" +
        "ZGFlbW9uEAISAAEAENIzNDw9Wkl0ZW1SZWNvcmSiPDfSMzQ/QFdOU0FycmF5oj830hMNQiCkQ0RFRoAQgBqAIoApgA7ZDUkl" +
        "SktMIiQjJ05PUFFSKlRVXWRldmVsb3Blck5hbWVUdXVpZFppZGVudGlmaWVyXxAQYnVuZGxlSWRlbnRpZmllcoANgBKAEYAU" +
        "gBaAE4ALgBeAGBALXEV4YW1wbGUgQ29ycF8QD2NvbS5leGFtcGxlLmFwcNJaDVtcXE5TLnV1aWRieXRlc08QEIo9nv06qkL+" +
        "kwoX44XENsWAFdIzNF5fVk5TVVVJRKJeN18QETIuY29tLmV4YW1wbGUuYXBwV0V4YW1wbGXTLA0tLi9lgACACYAZXxAnZmls" +
        "ZTovLy9BcHBsaWNhdGlvbnMvRXhhbXBsZSUyMEFwcC5hcHAv2A1oJUxLIiQjJ1FrbG1ub3BfEBBwYXJlbnRJZGVudGlmaWVy" +
        "gA2AFoAegByAH4AbgB2AIBAEXxASY29tLmV4YW1wbGUuaGVscGVyVkhlbHBlchAKXxAUNC5jb20uZXhhbXBsZS5oZWxwZXLT" +
        "LA0tLi95gACACYAhXxAmQ29udGVudHMvTGlicmFyeS9Mb2dpbkl0ZW1zL0hlbHBlci5hcHDVIw0kIiV8J35/T4AlgA2AI4Ak" +
        "gBFVQmFzZWQQCNMsDS2EL4aAJoAJgCjTLA0tLi+KgACACYAnXxAnZmlsZTovLy9BcHBsaWNhdGlvbnMvRXhhbXBsZSUyMEFw" +
        "cC5hcHAvXxA1Q29udGVudHMvTGlicmFyeS9MYXVuY2hBZ2VudHMvY29tLmV4YW1wbGUuYWdlbnQucGxpc3TTJCINjiongCqA" +
        "C4ANXU5vRGlzcG9zaXRpb27SMzSTlFxOU0RpY3Rpb25hcnmikzfSMzSWl1dTdG9yYWdlopY3AAgAEQAaACQAKQAyADcASQBM" +
        "AFIAVACEAIoAjwCWAK4AsACyALkAwQDMAM8A0QDTANYA2ADaANwBAwEqAS8BMQEzATUBQAFFAUkBTgFaAVwBXgFgAWIBZAFr" +
        "AXMBfwGBAYMBhQG+AcMBzgHXAd0B4AHpAf4CAAIFAgoCFQIYAh0CJQIoAi0CMgI0AjYCOAI6AjwCTwJdAmICbQKAAoIChAKG" +
        "AogCigKMAo4CkAKSApQCoQKzArgCxQLYAtoC3wLmAukC/QMFAwwDDgMQAxIDPANNA2ADYgNkA2YDaANqA2wDbgNwA3IDhwOO" +
        "A5ADpwOuA7ADsgO0A90D6APqA+wD7gPwA/ID+AP6BAEEAwQFBAcEDgQQBBIEFAQ+BHYEfQR/BIEEgwSRBJYEowSmBKsEswAA" +
        "AAAAAAIBAAAAAAAAAJgAAAAAAAAAAAAAAAAAAAS2";
}
#endif
