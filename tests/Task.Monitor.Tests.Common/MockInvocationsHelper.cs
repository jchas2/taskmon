using Moq;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Common;

public static class MockInvocationsHelper
{
    public static void WriteInvocations(IInvocationList list, ITestOutputHelper outputHelper)
    {
        outputHelper.WriteLine("Invocations:");
        
        foreach (var invocation in list) {
            outputHelper.WriteLine($"  {invocation.Method.Name}({string.Join(", ", invocation.Arguments)})");
        }
    }
}