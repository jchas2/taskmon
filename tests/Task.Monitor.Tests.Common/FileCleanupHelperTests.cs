namespace Task.Monitor.Tests.Common;

public class FileCleanupHelperTests
{
    [Fact]
    public void Should_Cleanup_Files()
    {
        string tempFile;
        
        using (FileCleanupHelper helper = new()) {
            tempFile = helper.GetTempFile(".tmp");
            File.WriteAllText(tempFile, "Text");
            Assert.True(File.Exists(tempFile));
        }

        Assert.False(File.Exists(tempFile));
    }
}

