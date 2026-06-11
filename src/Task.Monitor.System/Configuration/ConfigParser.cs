using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Task.Monitor.Internal.Abstractions;

namespace Task.Monitor.System.Configuration;

public class ConfigParser : IDisposable
{
    private const int EndOfFile = -1;
    private const int InitialStringSize = 32;
    private readonly TextReader reader;
    private readonly IList<ConfigSection> sections;
    
    public ConfigParser(string str)
    {
        reader = new StringReader(str);
        sections = new List<ConfigSection>();
    }

    public ConfigParser(IFileSystem fileSys, string path)
    {
        if (!fileSys.Exists(path)) {
            throw new FileNotFoundException(path);
        }

        string buffer = fileSys.ReadAllText(path);
        reader = new StringReader(buffer);
        sections = new List<ConfigSection>();
    }

    ~ConfigParser() => 
        Dispose();
    
    public void Dispose() =>
        // TODO: Unit Tests crash with Null Ref Exception here unless reader? nullable check performed
        // even though ctor guarantees non-null.
        reader?.Dispose();  

    public void Parse()
    {
        bool inComment = false;
        ConfigSection? section = null;

        while (true) {
            int character = reader.Read();

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

            if (ch == '#' || ch == ';') {
                inComment = true;
                continue;
            }

            if (ch == '[') {
                section = new ConfigSection();
                ParseSection(ref section);
                sections.Add(section);
                continue;
            }

            if (false == char.IsLetter(ch)) {
                break;
            }

            if (section != null) {
                ParseKey(ref section, ch);
                continue;
            }

            if (section == null) {
                break;
            }
        }
    }
    
    private void ParseSection(ref ConfigSection section)
    {
        StringBuilder buffer = new(InitialStringSize);
        
        while (true) {
            int character = reader.Read();
            char ch = (char)character;
            
            if (character == EndOfFile) {
                buffer.Append(ch);
                throw new ConfigParseException($"End-of-file found reading section name {buffer} before closing ']'.");
            }

            if (ch == ']') {
                break;
            }

            if (char.IsWhiteSpace(ch)) {
                buffer.Append(ch);
                throw new ConfigParseException($"Unexpected white space char in section name {buffer}.");
            }

            if (!(char.IsLetterOrDigit(ch) || ch == '-')) {
                buffer.Append(ch);
                throw new ConfigParseException($"Unexpected char in section name {buffer}. Must be alpha-numeric.");
            }

            buffer.Append(ch);
        }

        if (buffer.Length == 0) {
            throw new ConfigParseException("Section name cannot be empty.");
        }
        
        section.Name = buffer.ToString();
    }

    private void ParseKey(ref ConfigSection section, char initChar)
    {
        StringBuilder keyBuffer = new(InitialStringSize);
        keyBuffer.Append(initChar);

        char ch;
        
        while (true) {
            int character = reader.Read();
            ch = (char)character;
            
            if (character == EndOfFile) {
                keyBuffer.Append(ch);
                throw new ConfigParseException($"End-of-file found reading key name {keyBuffer}.");
            }
            
            if (!(char.IsLetterOrDigit(ch) || ch == '-')) {
                break;
            }

            keyBuffer.Append(ch);
        }

        while (ch == ' ' || ch == '\t') {
            ch = (char)(reader.Read());
        }

        if (ch != '=') {
            throw new ConfigParseException($"Expected '=' after key {keyBuffer}.");
        }

        // Keys are mandatory, values are not.
        if (keyBuffer.Length == 0) {
            throw new ConfigParseException("Key name cannot be empty.");
        }

        StringBuilder valueBuffer = new(InitialStringSize);
        ParseValue(ref valueBuffer);

        string key = keyBuffer.ToString().ToLower();
        string val = valueBuffer.ToString();

        if (false == section.Contains(key)) {
            section.Add(key, val);
        }
    }

    private void ParseValue(ref StringBuilder valueBuffer)
    {
        bool inComment = false;

        while (true) {
            int character = reader.Read();

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

            if (ch == '#' || ch == ';') {
                inComment = true;
                continue;
            }

            valueBuffer.Append(ch);
        }
    }

    public IList<ConfigSection> Sections => sections;
}
