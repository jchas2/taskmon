using System.Runtime.CompilerServices;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Internal.Abstractions.Tests;

public sealed class FileSystemTests
{
    private const string FileText = $@"
Line1
Line2";
        
    [Fact]
    public void Should_Create_File_And_WriteRead_Text()
    {
        using FileCleanupHelper cleanupHelper = new();
        string fileName = cleanupHelper.GetTempFile(".txt");
        
        FileSystem fileSystem = new();
        fileSystem.WriteAllText(fileName, FileText);
        string readText = fileSystem.ReadAllText(fileName);
        
        Assert.Equal(FileText, readText);
    }

    [Fact]
    public void Should_Create_File_And_Test_Exists()
    {
        using FileCleanupHelper cleanupHelper = new();
        string fileName = cleanupHelper.GetTempFile(".txt");
        
        FileSystem fileSystem = new();
        fileSystem.WriteAllText(fileName, FileText);
        bool exists = fileSystem.FileExists(fileName);
        
        Assert.True(exists);
    }
}
