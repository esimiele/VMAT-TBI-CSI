using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBIAutoPlanMT
{
    /// <summary>
    /// Interaction logic for AutorunProgress.xaml
    /// </summary>
    public partial class AutorunProgress : Window
    {
        ESAPIworker slave;
        public AutorunProgress(ESAPIworker e)
        {
            InitializeComponent();
            slave = e;
            doStuff();
        }

        public void doStuff()
        {
            slave.DoWork(d =>
            {
                //create an instance of the generateTS class, passing the structure sparing list vector, the selected structure set, and if this is the scleroderma trial treatment regiment
                //The scleroderma trial contouring/margins are specific to the trial, so this trial needs to be handled separately from the generic VMAT treatment type
                VMATTBIAutoPlanMT.generateTS generate;
                //overloaded constructor depending on if the user requested to use flash or not. If so, pass the relevant flash parameters to the generateTS class
                if (!d.useFlash) generate = new VMATTBIAutoPlanMT.generateTS(d.TS_structures, d.scleroStructures, d.structureSpareList, d.selectedSS, d.targetMargin, d.isScleroRegimen);
                else generate = new VMATTBIAutoPlanMT.generateTS(d.TS_structures, d.scleroStructures, d.structureSpareList, d.selectedSS, d.targetMargin, d.isScleroRegimen, d.useFlash, d.flashStructure, d.flashMargin);
                //if (generate.generateStructures(this, System.Windows.Threading.Dispatcher.CurrentDispatcher)) return;
                //does the structure sparing list need to be updated? This occurs when structures the user elected to spare with option of 'Mean Dose < Rx Dose' are high resolution. Since Eclipse can't perform
                //boolean operations on structures of two different resolutions, code was added to the generateTS class to automatically convert these structures to low resolution with the name of
                // '<original structure Id>_lowRes'. When these structures are converted to low resolution, the updateSparingList flag in the generateTS class is set to true to tell this class that the 
                //structure sparing list needs to be updated with the new low resolution structures.
                //if (generate.updateSparingList)
                //{
                //    clear_spare_list();
                //    //update the structure sparing list in this class and update the structure sparing list displayed to the user in TS Generation tab
                //    structureSpareList = generate.spareStructList;
                //    add_sp_volumes(selectedSS, structureSpareList);
                //}
                //if (generate.optParameters.Count() > 0) optParameters = generate.optParameters;
                //numIsos = generate.numIsos;
                //numVMATIsos = generate.numVMATIsos;
                //isoNames = generate.isoNames;
            });
        }

        public void provideUpdate(string message) { update.Text += message + System.Environment.NewLine; scroller.ScrollToBottom();}

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        private void Abort_Click(object sender, RoutedEventArgs e)
        {
            ////the user wants to stop the optimization loop. Set the abortOpt flag to true. The optimization loop will stop when it reaches an appropriate point
            //if (!isFinished)
            //{
            //    string message = System.Environment.NewLine + System.Environment.NewLine +
            //        " Abort command received!" + System.Environment.NewLine + " The optimization loop will be stopped at the next available stopping point!" + System.Environment.NewLine + " Be patient!";
            //    update.Text += message + System.Environment.NewLine;
            //    abortOpt = true;
            //    abortStatus.Text = "Canceling";
            //    abortStatus.Background = System.Windows.Media.Brushes.Yellow;
            //    updateLogFile(message);
            //}
        }
    }
}
