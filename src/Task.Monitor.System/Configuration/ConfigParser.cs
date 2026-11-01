using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Task.Monitor.Internal.Abstractions;

namespace Task.Monitor.System.Configuration;

public class ConfigParser : IDisposable
{
    private const int EndOfFile         = -1;
    private const int InitialStringSize = 32;
    
    private readonly TextReader           reader;
    private readonly IList<ConfigSection> sections;

    private int line = 1;
    private int previous;
    
    public ConfigParser(string str)
    {
        reader   = new StringReader(str);
        sections = new List<ConfigSection>();
    }

    public ConfigParser(IFileSystem fileSys, string path)
    {
        if (!fileSys.FileExists(path)) {
            throw new FileNotFoundException(path);
        }

        string buffer = fileSys.ReadAllText(path);
        
        reader   = new StringReader(buffer);
        sections = new List<ConfigSection>();
    }

    ~ConfigParser() => 
        Dispose();
    
    public void Dispose() =>
        // TODO: Unit Tests crash with Null Ref Exception here unless reader? nullable check performed
        // even though ctor guarantees non-null.
        reader?.Dispose();  

    private bool IsDuplicateSection(string name) =>
        sections.Any(s => s.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));

    public void Parse()
    {
        bool           inComment = false;
        ConfigSection? section = null;

        while (true) {
            int character = Read();

            if (character == EndOfFile) {
                break;
            }

            char ch = (char)character;

            if (ch == '\n') {
                inComment = false;
                continue;
            }

            if (inComment) {
                continue;
            }

            if (char.IsWhiteSpace(ch)) {
                continue;
            }

            if (ch == ';' || ch == '#') {
                inComment = true;
                continue;
            }

            if (ch == '[') {
                section = new ConfigSection();
                ParseSection(ref section);

                if (IsDuplicateSection(section.Name)) {
                    throw ParseError($"Section {section.Name} appears more than once.");
                }

                sections.Add(section);
                continue;
            }

            if (section == null || false == char.IsLetter(ch)) {
                throw ParseError($"Unexpected '{ch}'. Expected a section, a key or a comment.");
            }

            ParseKey(ref section, ch);
        }
    }

    private ConfigParseException ParseError(string message) => new($"Line {line}: {message}");

    private void ParseKey(ref ConfigSection section, char initChar)
    {
        StringBuilder keyBuffer = new(InitialStringSize);
        keyBuffer.Append(initChar);

        char ch;
        
        while (true) {
            int character = Read();
            
            ch = (char)character;
            
            if (character == EndOfFile) {
                throw ParseError($"End-of-file found reading key name {keyBuffer}.");
            }
            
            if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '.')) {
                break;
            }

            keyBuffer.Append(ch);
        }

        while (ch == ' ' || ch == '\t') {
            ch = (char)Read();
        }

        if (ch != '=') {
            throw ParseError($"Expected '=' after key {keyBuffer}.");
        }

        // Keys are mandatory, values are not.
        if (keyBuffer.Length == 0) {
            throw ParseError("Key name cannot be empty.");
        }

        StringBuilder valueBuffer = new(InitialStringSize);
        ParseValue(ref valueBuffer);

        string key = keyBuffer.ToString().ToLower();
        string val = valueBuffer.ToString().Trim();

        if (false == section.Contains(key)) {
            section.Add(key, val);
        }
    }

    private void ParseSection(ref ConfigSection section)
    {
        StringBuilder buffer = new(InitialStringSize);
        
        while (true) {
            int character = Read();
            char ch       = (char)character;
            
            if (character == EndOfFile) {
                throw ParseError($"End-of-file found reading section name {buffer} before closing ']'.");
            }

            if (ch == ']') {
                break;
            }

            if (char.IsWhiteSpace(ch)) {
                buffer.Append(ch);
                continue;
            }

            if (char.IsLetterOrDigit(ch) || ch == '-') {
                buffer.Append(ch);
                continue;
            }

            throw ParseError($"Unexpected char in section name {buffer}. Must be alpha-numeric.");
        }

        if (buffer.Length == 0) {
            throw ParseError("Section name cannot be empty.");
        }
        
        section.Name = buffer.ToString();
    }

    private void ParseValue(ref StringBuilder valueBuffer)
    {
        bool inComment = false;

        while (true) {
            int character = Read();

            if (character == EndOfFile) {
                return;
            }
            
            char ch = (char)character;
            
            if (ch == '\r' || ch == '\n') {
                return;
            }

            if (inComment) {
                continue;
            }
            
            if (char.IsWhiteSpace(ch)) {
                valueBuffer.Append(ch);
                continue;
            }

            if (ch == ';') {
                inComment = true;
                continue;
            }

            valueBuffer.Append(ch);
        }
    }

    // Counts a line when moving past its newline, so an error reported on reading the newline
    // still names the line it ends.
    private int Read()
    {
        if (previous == '\n') {
            line++;
        }

        previous = reader.Read();
        return previous;
    }

    public IList<ConfigSection> Sections => sections;
}
