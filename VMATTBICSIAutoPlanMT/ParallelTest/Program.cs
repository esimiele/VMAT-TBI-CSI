using System;
using System.Linq;
using System.Diagnostics;
using VMS.TPS.Common.Model.API;

[assembly: ESAPIScript(IsWriteable = true)]
namespace ParallelTest
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ConsoleTraceListener myWriter = new ConsoleTraceListener(true);
            Debug.Listeners.Add(myWriter);
            Debug.AutoFlush = true;
            try
            {
                using (Application app = Application.CreateApplication())
                {
                    Execute(app, args);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }
            Console.ReadLine();

        }

        static void Execute(Application app, string[] args)
        {
            string mrn = args[0];
            string uid = args[1];
            try
            {
                Patient pat = app.OpenPatientById(mrn);
                ExternalPlanSetup plan = pat.Courses.SelectMany(x => x.ExternalPlanSetups).FirstOrDefault(x => x.UID == uid);
                Console.WriteLine(plan.Id);
                try
                {
                    pat.BeginModifications();
                    CalculationResult calcRes = plan.CalculateDose();
                    if (!calcRes.Success)
                    {
                        Console.WriteLine("Dose calculation");
                        return;
                    }
                }
                catch (Exception except)
                {
                    Console.WriteLine(except.Message);
                    return;
                }
                app.SaveModifications();
            }
            catch (Exception e) { Console.WriteLine(e.Message); } 
        }
    }
}


