/*
User input :
The user must provide 2 files (file names MUST be id.txt and index.txt)
1.
id.txt file that contains only one column with patient IDs
2. 
index.txt contain the chosen indexes that the user wants to collect
Each structure on a line with a "," to separate the fields
First field is the structure name
if the struct can have several version of structure name, separate them with a ;
If several versions of a structure name are provided AND several structures really
exists with these names, only the first is taken in account

Other fields can be different indexes. Candidates are:
vol --> volume of the structure (Vol and VOL are tolerated)
min --> minimum dose of the structure (Gy) (Min and MIN are tolerated)
max --> max dose of the structure (Gy) (Max and MAX are tolerated)
mean --> mean dose of the structure (Gy) (Mean and MEAN are tolerated)
median --> median dose of the structure (Gy) (Median and MEDIAN are tolerated)
DXX% or DXXcc --> e.g. D95% or D2.5cc : Dose (Gy) recieved by 95% or 2.55 cc of the structure
VXX% or VXXcc --> e.g V49.6% or V49.6cc : Volume in % or cc that recieved 49.6 Gy

No relative doses are allowed. 

In the following exemple the user wants to collect :
The max dose and the Dose (Gy) recieved by 95% of the volume for structure "Coeur"
The max dose for structure "Canal med"
The max dose for structure that can be name "ptvCMI" OR "ptv cmi"  

----
Coeur,max,D95%
Canal med,max
ptvCMI;ptv cmi,max 

Output: (in out/ dir.)
log.txt: main information about the execution
data.csv: all collected data

 */
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Threading;
using System.IO;

namespace VMS.TPS
{
    class Program
    {
        [STAThread]
        #region EMPTY MAIN PROGRAM
        static void Main(string[] args)
        {
            try
            {
                using (Application app = Application.CreateApplication())
                {
                    Execute(app);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }
        }
        #endregion

        #region EXECUTE PROGRAM, THE REAL MAIN
        static void Execute(Application app)
        {
            #region WELCOME MESSAGE
            Console.WriteLine("-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-");
            Console.WriteLine("");
            Console.WriteLine("     D O S E   H U N T E R ");
            Console.WriteLine("");
            Console.WriteLine("        Luc SIMON, 2021");
            Console.WriteLine("");
            Console.WriteLine("-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-");
            #endregion

            #region DECLARATION OF VARIABLES
            List<string> list_patient = new List<string>();
            List<string> list_struct = new List<string>();
            List<string> list_struct_name = new List<string>();
            String line;
            string[] lineElements;
            string[] filterTags;
            int verbose;
            verbose = 1;

            int nPatient = 0;  // total number of patient. Must be the number of lines in ip.txt
            int nPatientWithAnAcceptedPlan = 0; // number of patient with at least an accepted plan
            int foundOneAcceptedPlan = 0; // bool, use to count nPatientWithAnAcceptedPlan
            int totalNumberOfPlans = 0;
            int numberOfAcceptedPlans = 0;
            int numberOfPlansForThisPatient = 0;
            int numberOfAcceptedPlansForThisPatient = 0;          
            string idfilename = "id.txt"; // Input file names can not be chosen
            string structsfilename = "index.txt"; // Input file names can not be chosen
            string planfilterfilename = "planfilter.txt"; // Input file names can not be chosen
            Structure struct1;
            double minTotalDose, maxTotalDose;
            bool keepUnapprovedPlan,keepPAapprovedPlan,keepTAapprovedPlan,keepNamedPlan,keepUnamedPlan;
            bool keepIfPlanNameContainAstring, excludeIfPlannedNameContainAString;
            string stringToContainToBeKept, stringToContainToBeExcluded;
            #endregion

            #region READ THE ID FILE
            if (verbose > 5)
            {
                Console.WriteLine("ID FILE OPEN.....START\n");
                Console.ReadLine();
            }
            // Open a text file to read patient ID. Text file must contains IDs, one by line
            if (!File.Exists(idfilename))
            {
                Console.WriteLine("Can't find file {0}\nPlease ENTER to exit\n", idfilename);
                Console.ReadLine();
                return;
            }
            StreamReader sr = new StreamReader(idfilename);

            line = sr.ReadLine();
            list_patient.Add(line);
            while (line != null)
            {
                line = sr.ReadLine();
                if (line != null)
                    list_patient.Add(line);
            }
            sr.Close();

            if (verbose > 5)
            {
                Console.WriteLine("ID FILE OPEN.....OK\n");
                Console.ReadLine();
            }

            #endregion

            #region READ THE PLAN FILTER FILE
            if (verbose > 5)
            {
                Console.WriteLine("PLAN FILTER FILE OPEN.....START\n");
                Console.ReadLine();
            }
            // Open a text file to read patient ID. Text file must contains IDs, one by line

            // DEFAULT FILTER VALUES :
            minTotalDose = 60.0;
            maxTotalDose = 80.0;
            keepNamedPlan = true;
            keepUnamedPlan = true;
            keepPAapprovedPlan = false;
            keepTAapprovedPlan = false;
            keepUnapprovedPlan = true;
            keepIfPlanNameContainAstring = false;
            excludeIfPlannedNameContainAString = false;
            stringToContainToBeKept = "toto";
            stringToContainToBeExcluded = "toto";

            if (!File.Exists(planfilterfilename))
            {
                Console.WriteLine("Can't find file {0}\r\n", planfilterfilename);
                Console.WriteLine("Default filters will be used\r\n");

                Console.ReadLine();

            }
            else
            {
                StreamReader srf = new StreamReader(planfilterfilename);
                line = "start";
                while (line != null)
                {
                    line = srf.ReadLine();
                    if (line != null)
                    {
                        filterTags = line.Split(':');
                        if (filterTags[0] == "Min Total Dose (Gy)")
                        {
                            minTotalDose = Convert.ToDouble(filterTags[1]);
                        }
                        if (filterTags[0] == "Max Total Dose (Gy)")
                        {
                            maxTotalDose = Convert.ToDouble(filterTags[1]);
                        }
                        if (filterTags[0] == "TreatApproved plan")
                        {
                            if (filterTags[1] == "yes")
                                keepTAapprovedPlan = true;
                            else if (filterTags[1] == "no")
                                keepTAapprovedPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "PlanningApproved plan")
                        {
                            if (filterTags[1] == "yes")
                                keepPAapprovedPlan = true;
                            else if (filterTags[1] == "no")
                                keepPAapprovedPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Unapproved plan")
                        {
                            if (filterTags[1] == "yes")
                                keepUnapprovedPlan = true;
                            else if (filterTags[1] == "no")
                                keepUnapprovedPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Named plan")
                        {
                            if (filterTags[1] == "yes")
                                keepNamedPlan = true;
                            else if (filterTags[1] == "no")
                                keepNamedPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Unnamed plan")
                        {
                            if (filterTags[1] == "yes")
                                keepUnamedPlan = true;
                            else if (filterTags[1] == "no")
                                keepUnamedPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Plan name must contain a string")
                        {
                            if (filterTags[1] == "yes")
                            {
                                keepIfPlanNameContainAstring = true;
                                stringToContainToBeKept = filterTags[2];
                            }
                            else if (filterTags[1] == "no")
                                keepIfPlanNameContainAstring = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Exclude if plan name contains")
                        {
                            if (filterTags[1] == "yes")
                            {
                                excludeIfPlannedNameContainAString = true;
                                stringToContainToBeExcluded = filterTags[2];
                            }
                            else if (filterTags[1] == "no")
                                excludeIfPlannedNameContainAString = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                    }
                }
                srf.Close();
            }

            
            #region RECAP FILTERS
            Console.WriteLine("\r\n\r\nPlans filters. The following plans will be used to select the plans:");
            Console.WriteLine("Total dose between {0:0.00} and {1:0.00} Gy", minTotalDose, maxTotalDose);
            Console.WriteLine("Keep planning approved plans?\t{0}", keepPAapprovedPlan);
            Console.WriteLine("Keep treatment approved plans?\t{0}", keepTAapprovedPlan);
            Console.WriteLine("Keep unapproved plans?\t{0}", keepUnapprovedPlan);
            Console.WriteLine("Keep plans containing a particular string?\t{0}", keepIfPlanNameContainAstring);
            if (keepIfPlanNameContainAstring)
                Console.WriteLine(" String is: '{0}'", stringToContainToBeKept);
            Console.WriteLine("Exclude plans containing a particular string?\t{0}", excludeIfPlannedNameContainAString);
            if (excludeIfPlannedNameContainAString)
                Console.WriteLine(" String is: '{0}'", stringToContainToBeExcluded);
            Console.WriteLine("\r\n\r\n\r\n");
            #endregion

            #endregion

            #region READ THE FILE WITH STRUCTURES AND METRICS
            if (verbose > 5)
            {
                Console.WriteLine("METRICS FILE OPEN.....START\n");
                Console.ReadLine();
            }
            // Open a text file to read patient ID. Text file must contains IDs, one by line
            if (!File.Exists(structsfilename))
            {
                Console.WriteLine("Can't find file {0}\nPlease ENTER to exit\n", structsfilename);
                Console.ReadLine();
                return;
            }
            StreamReader srm = new StreamReader(structsfilename);

            if (srm == null)
            {
                Console.WriteLine("Impossible to read {0}\r\n The file is maybe already opened\r\n", structsfilename);
                Console.ReadLine();
                return;
            }


            line = "start";

            while (line != null)
            {
                line = srm.ReadLine();
                if (line != null)
                    list_struct.Add(line);
                if (verbose > 5)
                    Console.WriteLine("line:{0}", line);

                if (line != null)
                {                   
                    lineElements = line.Split(','); // lineElements is a list of the elements of a line 
                    list_struct_name.Add(lineElements[0]); // first column is the structure name
                    if (verbose > 5)
                    {
                        Console.WriteLine("struct:{0}", lineElements[0]);
                        Console.ReadLine();
                    }
                }
            }

            srm.Close();

            if (verbose > 5)
            {
                Console.WriteLine("METRICS FILE OPEN.....OK\n");
                Console.ReadLine();
            }

            #endregion

            #region DELETE AND RECREATE OUTPUT DIR
            string folderPath = @"./out";
            if (!Directory.Exists(folderPath)) // if out/ doesn't exist
            {
                Directory.CreateDirectory(folderPath);
                Console.WriteLine("Directory {0} created...", folderPath);
            }
            else // if out/ already exists
            {
                var dir = new DirectoryInfo(folderPath);
                foreach (var file in dir.GetFiles()) // get files one by one in out/ to delete them
                {
                    {
                        try
                        {
                            file.Delete(); // delete this file
                        }
                        catch (IOException)  // This part does not work. If an output file is open the error message is not displayed
                        {
                            Console.WriteLine("Impossible to delete a file (locked). Please close all output files\r\n");
                            Console.ReadLine();
                            return;
                        }
                    }
                }
                Directory.Delete(folderPath); // Remove out/ dir. 
                Directory.CreateDirectory(folderPath); // Re Create out/ dir. 
                Console.WriteLine("Directory {0} deleted and recreated...", folderPath);
            }
            #endregion

            #region CREATE THE OUTPUT FILES         
            // create log file
            StreamWriter swLogFile = new StreamWriter("out/log.txt");
            swLogFile.WriteLine("Output log\r\n\r\n\r\n");
            
            // create file for output data
            StreamWriter swData = new StreamWriter("out/data.csv");

            #region WRITE CSV HEAD
            //swData.Write("ID,date,user");  // first 3 fields separated by a coma
            swData.Write("patientID;planID;date;user");  // first 3 fields separated by a ;
            foreach (string myString in list_struct) // loop on the lines
            {
                lineElements = myString.Split(',');  // separate elements in a line by a ,
                string[] myFirstName = lineElements[0].Split(';'); // separate the element (different struct names separate by a ;) 
                foreach (string myOthereMetrics in lineElements.Skip(1)) // Create a cell name: <struct name> (<dose index>)
                    swData.Write(";{0} ({1})", myFirstName[0], myOthereMetrics);
                //swData.Write(",{0} ({1})", myFirstName[0], myOthereMetrics);
            }
            swData.Write("\r\n"); // add a final line break
            #endregion
            #endregion

            #region LOOP EVERY PATIENT
            foreach (string ipp in list_patient) // loop on the patient list
            {
                nPatient++; // number of patients
                numberOfPlansForThisPatient = 0;
                numberOfAcceptedPlansForThisPatient = 0;
                foundOneAcceptedPlan = 0;

                Patient p = app.OpenPatientById(ipp); // open the patient

                if (verbose > 0)
                {
                    Console.WriteLine("{1} {0}", p.Name, nPatient); // verbose
                    swLogFile.WriteLine("{1} {0}\n\n\n", p.Name, nPatient);
                }
                int keepThisPlan = 1;
                #region LOOP EVERY COURSE
                foreach (Course course in p.Courses) // loop on the courses
                {
                    #region LOOP EVERY PLAN
                    foreach (PlanSetup plan in course.PlanSetups) // loop on the plans
                    {
                        keepThisPlan = 1;
                        totalNumberOfPlans++;
                        numberOfPlansForThisPatient++;
                        Console.WriteLine("Plan: {0} ", plan.Id); // Verbose      
                        swLogFile.WriteLine("Plan: {0} ", plan.Id); // Verbose      

                        #region TEST THE PLAN

                        #region TEST IF THE PLAN HAS A NAME

                     
                        if (keepNamedPlan == false) // dont keep  plans with a name
                        {
                            if (plan.Name != "")  // if  name exist
                            {
                                keepThisPlan = keepThisPlan * 0;
                                Console.WriteLine("         refused: THE PLAN HAS A NAME");
                                swLogFile.WriteLine("         refused: THE PLAN HAS A NAME ");
                            }                          
                        }
                        if (keepUnamedPlan == false) // dont keep plans with no name
                        {
                            if (plan.Name == "")  // if  no name 
                            {
                                keepThisPlan = keepThisPlan * 0;
                                Console.WriteLine("         refused: THE PLAN HAS NO NAME");
                                swLogFile.WriteLine("         refused: THE PLAN HAS NO NAME ");
                            }
                        }
                        #endregion
                        #region TEST IF THE PLAN APPROBATION
                        if (keepTAapprovedPlan == false) // dont keep  Treat approved plans
                        {
                            if (plan.ApprovalStatus == PlanSetupApprovalStatus.TreatmentApproved)  // if  treat approve
                            {
                                keepThisPlan = keepThisPlan * 0;
                                Console.WriteLine("         refused: THE PLAN IS TREAT APPROVED");
                                swLogFile.WriteLine("         refused: THE PLAN IS TREAT APPROVED");
                            }
                        }
                        if (keepPAapprovedPlan == false) // dont keep  planning approved plans
                        {
                            if (plan.ApprovalStatus == PlanSetupApprovalStatus.PlanningApproved)  // if  plan approve
                            {
                                keepThisPlan = keepThisPlan * 0;
                                Console.WriteLine("         refused: THE PLAN IS PLAN APPROVED");
                                swLogFile.WriteLine("         refused: THE PLAN IS PLAN APPROVED");
                            }
                        }
                        if (keepUnapprovedPlan == false) // dont keep   unapproved plans
                        {
                            if (plan.ApprovalStatus == PlanSetupApprovalStatus.UnApproved)  // if  plan approve
                            {
                                keepThisPlan = keepThisPlan * 0;
                                Console.WriteLine("         refused: THE PLAN IS UNAPPROVED");
                                swLogFile.WriteLine("         refused: THE PLAN IS UNAPPROVED");
                            }
                        }
                        #endregion
                        #region TEST IF TOTAL DOSE BETWEEN MIN AND MAX
                        if (plan.TotalDose.Dose < minTotalDose || plan.TotalDose.Dose > maxTotalDose)
                            {
                                keepThisPlan = keepThisPlan * 0;
                                Console.WriteLine("         refused: TOTAL DOSE {0} is not between {1} and {2}",plan.TotalDose.Dose,minTotalDose,maxTotalDose);
                                swLogFile.WriteLine("         refused: TOTAL DOSE {0} is not between {1} and {2}", plan.TotalDose.Dose, minTotalDose, maxTotalDose);
                            }
                        #endregion
                        #region TEST IF PLAN CONTAINS OR DOES NOT CONTAIN A CHOSEN STRING
                        if (keepIfPlanNameContainAstring)
                        {
                            if (plan.Id.Contains(stringToContainToBeKept))
                                keepThisPlan = keepThisPlan * 1;
                            else
                            {
                                keepThisPlan = keepThisPlan * 0;
                                Console.WriteLine("         refused: plan ID ({0}) does not contain '{1}'", plan.Id,stringToContainToBeKept);
                                swLogFile.WriteLine("         refused: plan ID ({0}) does not contain '{1}'", plan.Id, stringToContainToBeKept);

                            }
                        }

                        if (excludeIfPlannedNameContainAString)
                        {
                            if (plan.Id.Contains(stringToContainToBeExcluded))
                            {
                                keepThisPlan = keepThisPlan * 0;
                                Console.WriteLine("         refused: plan ID ({0}) does contain '{1}'", plan.Id, stringToContainToBeExcluded);
                                swLogFile.WriteLine("         refused: plan ID ({0}) does  contain '{1}'", plan.Id, stringToContainToBeExcluded);

                            }
                            else
                                keepThisPlan = keepThisPlan * 1;
                        }
                       
                        #endregion

                        #endregion

                        #region GET THE DATA 

                        if (keepThisPlan == 1)
                        {
                            numberOfAcceptedPlans++;
                            numberOfAcceptedPlansForThisPatient++;

                            if (foundOneAcceptedPlan == 0)
                            {
                                nPatientWithAnAcceptedPlan++;
                                foundOneAcceptedPlan = 1;
                            }
                            else
                            {
                                Console.WriteLine("   THIS PATIENT HAS MORE THAN ONE ACCEPTED PLAN !!"); // verbose
                                swLogFile.WriteLine("   THIS PATIENT HAS MORE THAN ONE ACCEPTED PLAN !!"); // verbose

                            }

                            if (verbose > 0)
                            {
                                Console.WriteLine("   Total dose =  {0}  ", plan.TotalDose); // verbose
                                swLogFile.WriteLine("   Total dose =  {0}  ", plan.TotalDose); // verbose
                            }

                            // write first 3 columns
                           // swData.Write("{0},{1},{2}", p.Id, plan.CreationDateTime, plan.CreationUserName);
                            swData.Write("{0};{1};{2};{3}", p.Id, plan.Id,plan.CreationDateTime, plan.CreationUserName);

                            StructureSet ss = plan.StructureSet;
                            bool foundOneStruct = false;
                            foreach (string myString in list_struct) // loop on lines of user dose index (1 by struct)
                            {
                                // get separated elements of a line (separator is a ,)
                                lineElements = myString.Split(',');
                                // get the different possible names of the structure (separate by a ;)
                                string[] myFirstName = lineElements[0].Split(';');  
                                foundOneStruct = false;
                                foreach (string myDiffStrucNames in myFirstName) // loop on the different names of a same struct
                                {
                                    if (foundOneStruct == false)
                                    {
                                        struct1 = ss.Structures.FirstOrDefault(x => x.Id == myDiffStrucNames);
                                        if (struct1 != null) // does the stucture exist?
                                        {
                                            if (!struct1.IsEmpty) // Is it empty?
                                            {
                                                foundOneStruct = true;
                                                DVHData dvh = plan.GetDVHCumulativeData(struct1, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);

                                                swLogFile.WriteLine("{0} found", myDiffStrucNames); // verbose
                                                if (verbose > 0)
                                                    Console.WriteLine(" {0} found", myDiffStrucNames);
                                                foreach (string dataToGet in lineElements.Skip(1)) // loop on index
                                                {
                                                    if (verbose > 5) 
                                                        Console.WriteLine(" Gimme the {0} for {1}\r\n", dataToGet, struct1.Id);

                                                    double thisValueImLookingFor = -99.999;
                     
                                                    thisValueImLookingFor = gimmeThatBro(dataToGet, struct1, plan,dvh);
                                                    
                                                    swLogFile.WriteLine("  {0} for {1} is {2:0.00}", dataToGet, struct1.Id, thisValueImLookingFor);
                                                    swData.Write(";{0:0.00}", thisValueImLookingFor);
                                                    //swData.Write(",{0:0.00}", thisValueImLookingFor);
                                                }
                                            }
                                        }
                                    }                                                                      
                                }
                                
                                if (foundOneStruct == false)
                                {
                                    Console.WriteLine(" Cannot find the structure {0} with this name or other names", myFirstName[0]);
                                    swLogFile.WriteLine(" Cannot find the structure {0} with this name or other names", myFirstName[0]);
                                    foreach (string skippedData in lineElements.Skip(1))
                                        swData.Write(";");
                                    //swData.Write(",");
                                }                              
                            }                            
                        }
                        swData.Write("\r\n");
                        #endregion

                    } //end of plan loop
                    #endregion
                } // end of course loop
                #endregion

                app.ClosePatient();
                Console.WriteLine("For this patient {0}/{1} accepted plans\n", numberOfAcceptedPlansForThisPatient, numberOfPlansForThisPatient);
                swLogFile.WriteLine("For this patient {0}/{1} accepted plans\n", numberOfAcceptedPlansForThisPatient, numberOfPlansForThisPatient);
            } // end of patient loop
            #endregion

            #region FINAL MESSAGE
            if (verbose > 0)
            {
                Console.WriteLine("Number of accepted/total patients: {1}/{0} (accepted : at least one accepted plan)", nPatient, nPatientWithAnAcceptedPlan);
                Console.WriteLine("Number of accepted/total plans: {0}/{1}", numberOfAcceptedPlans, totalNumberOfPlans);
                Console.WriteLine("Pleas type Enter\n");
                Console.ReadLine(); // Ask user to type enter to finish.
                swLogFile.WriteLine("Number of accepted/total patients: {1}/{0} (accepted : at least one accepted plan)", nPatient, nPatientWithAnAcceptedPlan);
                swLogFile.WriteLine("Number of accepted/total plans: {0}/{1}", numberOfAcceptedPlans, totalNumberOfPlans);
                swLogFile.WriteLine("Pleas type Enter\n");

            }
            #endregion

            #region CLOSE FILES
            swLogFile.Close();
            swData.Close();

            #endregion
        }
        #endregion

        #region AN EXTERNAL FUCTION USING REGEX TO GET THE DATA
        public static double gimmeThatBro(string myDataToGet, Structure myStruct, PlanSetup myPlan, DVHData dvh)
        {
            int verbose = 0;
            double checkThat = -1.0;
            if (verbose > 5) 
                Console.WriteLine("--> looking for {0} for {1} in {2}", myDataToGet, myStruct.Id,myPlan.Id);
            #region MAX DOSE       
            if (myDataToGet == "max" || myDataToGet == "Max" || myDataToGet == "MAX")
            {

                var myMaxDose = dvh.MaxDose;
                checkThat = myMaxDose.Dose;
            }
            #endregion
            #region MIN DOSE       
            if (myDataToGet == "min" || myDataToGet == "Min" || myDataToGet == "MIN")
            {
                var myMinDose = dvh.MinDose;
                checkThat = myMinDose.Dose;
            }
            #endregion
            #region MEDIAN DOSE
            if (myDataToGet == "median" || myDataToGet == "Median" || myDataToGet == "MEDIAN")
            {
                DoseValue myMedianDose = myPlan.GetDoseAtVolume(myStruct, 50, VolumePresentation.Relative, DoseValuePresentation.Absolute);              
                checkThat = myMedianDose.Dose;
            }
            #endregion
            #region MEAN DOSE
            if (myDataToGet == "mean" || myDataToGet == "Mean" || myDataToGet == "MEAN")
            {
                var myMeanDose = dvh.MeanDose;
                checkThat = myMeanDose.Dose;
            }
            #endregion
            #region VOLUME
            if (myDataToGet == "vol" || myDataToGet == "Vol" || myDataToGet == "VOL")
            {
                checkThat = myStruct.Volume;
            }
            #endregion
            #region D__% or D__cc
            string d_at_v_pattern = @"^D(?<evalpt>\d+\p{P}\d+|\d+)(?<unit>(%|cc))$"; // matches D95%, D2cc
            var testMatch = Regex.Matches(myDataToGet, d_at_v_pattern);
            if (testMatch.Count != 0) // count is 1 if D95% or D2cc
            {
                Group eval = testMatch[0].Groups["evalpt"];
                Group unit = testMatch[0].Groups["unit"];
                DoseValue.DoseUnit du = DoseValue.DoseUnit.Gy;
                DoseValue myD_something = new DoseValue(1000.1000, du);
                //DoseValue myD_something;
                double myD = Convert.ToDouble(eval.Value);
                if (unit.Value == "%")
                {
                    myD_something = myPlan.GetDoseAtVolume(myStruct, myD, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                    checkThat = myD_something.Dose;
                }
                else if (unit.Value == "cc")
                {
                    myD_something = myPlan.GetDoseAtVolume(myStruct, myD, VolumePresentation.AbsoluteCm3, DoseValuePresentation.Absolute);
                    checkThat = myD_something.Dose;
                }
                else
                    checkThat = -1.0;

                if (verbose > 5)
                    Console.WriteLine("Dxx {0:0.00} {1}", myD_something.Dose, myD_something.Unit);
            }
            #endregion
            #region V__Gy
            string v_at_d_pattern = @"^V(?<evalpt>\d+\p{P}\d+|\d+)(?<unit>(%|cc))$"; // matches V50.4cc or V50.4% 
            //var
            testMatch = Regex.Matches(myDataToGet, v_at_d_pattern);
            if (testMatch.Count != 0) // count is 1
            {
                Group eval = testMatch[0].Groups["evalpt"];
                Group unit = testMatch[0].Groups["unit"];
                DoseValue.DoseUnit du = DoseValue.DoseUnit.Gy;
                DoseValue myRequestedDose = new DoseValue(Convert.ToDouble(eval.Value), du);

                if (unit.Value == "cc")
                    checkThat = myPlan.GetVolumeAtDose(myStruct, myRequestedDose, VolumePresentation.AbsoluteCm3);
                else if (unit.Value == "%") 
                    checkThat = myPlan.GetVolumeAtDose(myStruct, myRequestedDose, VolumePresentation.Relative);
                else
                    checkThat = -1.0;
            }
            #endregion
            if (checkThat == -1.0)
                Console.WriteLine("Impossible to obtain {0} for {1} in {2} ", myDataToGet, myStruct.Id, myPlan.Id);
            return (checkThat);
        }
        #endregion
    }
}
