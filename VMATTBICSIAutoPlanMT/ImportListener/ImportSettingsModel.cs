using System.Collections.Generic;
using System.Linq;

namespace ImportListener
{
    public class ImportSettingsModel
    {
        public string ImportPath { get { return _importPath; } set { _importPath = value; } }
        public string MRN { get { return _mrn; } set { _mrn = value; } }
        public string AriaDBAET { get { return _ariaDBAET; } set { _ariaDBAET = value; } }
        public string AriaDBIP { get { return _ariaDBIP; } set { _ariaDBIP = value; } }
        public int AriaDBPort { get { return _ariaDBPort; } set { _ariaDBPort = value; } }
        public string LocalAET { get { return _localAET; } set { _localAET = value; } }
        public int LocalPort { get { return _localPort; } set { _localPort = value; } }
        public double TimeoutSec { get { return _timeOutSec; } set { _timeOutSec = value; } }
        public bool ParseError { get { return _parseError; } }

        private string _importPath;
        private string _mrn;
        private string _ariaDBAET;
        private string _ariaDBIP;
        private int _ariaDBPort;
        private string _localAET;
        private int _localPort;
        //timeout in seconds (30 mins by default)
        private double _timeOutSec = 30 * 60.0;
        private bool _parseError = true;

        public ImportSettingsModel(string[] args)
        {
            //args = new string[] { "\\\\shariatscap105\\Dicom\\RSDCM\\Import\\", "CSI55", "VMSDBD" ,"10.151.176.60" ,"51402" ,"DCMTK" ,"50400" ,"3600" };
            if (args.Any() && !ParseInputArguments(args.ToList())) _parseError = false;
            else
            {
                //prompt user to enter values
            }
        }

        /// <summary>
        /// Simple logic to parse the input string array of arguments. At least 7 arguments must be passed, the 8th is optional
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool ParseInputArguments(List<string> args)
        {
            if (args.Count < 7) return true;
            _importPath = args.ElementAt(0);
            _mrn = args.ElementAt(1);
            _ariaDBAET = args.ElementAt(2);
            _ariaDBIP = args.ElementAt(3);
            _ariaDBPort = int.Parse(args.ElementAt(4));
            _localAET = args.ElementAt(5);
            _localPort = int.Parse(args.ElementAt(6));
            if (args.Count() == 8) _timeOutSec = double.Parse(args.ElementAt(7));
            return false;
        }
    }
}
