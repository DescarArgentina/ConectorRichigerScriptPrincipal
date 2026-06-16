using System;
using System.IO;
using System.Text;

namespace WEB_SERVICE_RICHIGER
{
    public class MultiTextWriter : TextWriter
    {
        private TextWriter _consoleWriter;
        private TextWriter _fileWriter;

        public MultiTextWriter(TextWriter consoleWriter, TextWriter fileWriter)
        {
            _consoleWriter = consoleWriter;
            _fileWriter = fileWriter;
        }

        public override Encoding Encoding => _consoleWriter.Encoding;

        public override void Write(char value)
        {
            _consoleWriter.Write(value);
            _fileWriter.Write(value);
        }

        public override void Write(string value)
        {
            _consoleWriter.Write(value);
            _fileWriter.Write(value);
        }

        public override void WriteLine(string value)
        {
            _consoleWriter.WriteLine(value);
            _fileWriter.WriteLine(value);
        }

        public override void Flush()
        {
            _consoleWriter.Flush();
            _fileWriter.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // No disponemos _consoleWriter porque es la salida estándar
                _fileWriter?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
