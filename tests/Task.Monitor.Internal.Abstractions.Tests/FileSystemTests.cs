using System.Runtime.CompilerServices;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Internal.Abstractions.Tests;

public sealed class FileSystemTests
{
    private const string FileText = $@"
Line1
Line2";

    [Fact]
    public void DeleteFile_Removes_The_File()
    {
        using FileCleanupHelper cleanupHelper = new();
        string fileName = cleanupHelper.GetTempFile(".ini");

        FileSystem fileSystem = new();
        fileSystem.WriteAllText(fileName, "text");
        fileSystem.DeleteFile(fileName);

        Assert.False(fileSystem.FileExists(fileName));
    }

    [Fact]
    public void ReplaceAllText_Replaces_The_Text_And_Keeps_A_Backup()
    {
        using FileCleanupHelper cleanupHelper = new();
        string fileName = cleanupHelper.GetTempFile(".ini");

        FileSystem fileSystem = new();
        fileSystem.WriteAllText(fileName, "old");
        fileSystem.ReplaceAllText(fileName, "new");

        Assert.Equal("new", fileSystem.ReadAllText(fileName));
        Assert.Equal("old", fileSystem.ReadAllText($"{fileName}.bak"));
        Assert.False(fileSystem.FileExists($"{fileName}.tmp"));
    }

    [Fact]
    public void ReplaceAllText_Writes_Through_A_Symlink()
    {
        using FileCleanupHelper cleanupHelper = new();
        string target = cleanupHelper.GetTempFile(".ini");
        string link   = cleanupHelper.GetTempFile(".link");

        FileSystem fileSystem = new();
        fileSystem.WriteAllText(target, "old");

        try {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            // Windows needs developer mode or elevation to create one.
            return;
        }

        fileSystem.ReplaceAllText(link, "new");

        Assert.NotNull(new FileInfo(link).LinkTarget);
        Assert.Equal("new", fileSystem.ReadAllText(target));
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
    public void WriteAllTextAtomic_Creates_A_New_File()
    {
        using FileCleanupHelper cleanupHelper = new();
        string fileName = $"{cleanupHelper.GetTempFile(".ini")}.new";

        FileSystem fileSystem = new();
        fileSystem.WriteAllTextAtomic(fileName, "new");

        Assert.Equal("new", fileSystem.ReadAllText(fileName));
        Assert.False(fileSystem.FileExists($"{fileName}.tmp"));
        fileSystem.DeleteFile(fileName);
    }

    [Fact]
    public void WriteAllTextAtomic_Overwrites_And_Leaves_No_Backup()
    {
        using FileCleanupHelper cleanupHelper = new();
        string fileName = cleanupHelper.GetTempFile(".ini");

        FileSystem fileSystem = new();
        fileSystem.WriteAllText(fileName, "old");
        fileSystem.WriteAllTextAtomic(fileName, "new");

        Assert.Equal("new", fileSystem.ReadAllText(fileName));
        Assert.False(fileSystem.FileExists($"{fileName}.bak"));
        Assert.False(fileSystem.FileExists($"{fileName}.tmp"));
    }
}
