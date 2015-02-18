// ****************************************************************
// Copyright 2007, Charlie Poole
// This is free software licensed under the NUnit license. You may
// obtain a copy of the license at http://nunit.org
// ****************************************************************
using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Specialized;
using NUnit.Core;
using NUnit.Util;

namespace NUnit.ConsoleRunner
{
	/// <summary>
	/// Summary description for EventCollector.
	/// </summary>
	public class EventCollector : MarshalByRefObject, EventListener
	{
		private int testRunCount;
		private int testIgnoreCount;
		private int failureCount;
		private int level;

		private ConsoleOptions options;
		private TextWriter outWriter;
		private TextWriter errorWriter;

		StringCollection messages;
		
		private bool progress = false;
		private string currentTestName;

		private ArrayList unhandledExceptions = new ArrayList();
		private ArrayList describes = new ArrayList();

        private static string FormatMessage(string s)
        {
            if(s != null)
            {
                return s.Replace("\n", "<:LF:>");
            }
            else
            {
                return "";
            }
        }

		public EventCollector( ConsoleOptions options, TextWriter outWriter, TextWriter errorWriter )
		{
			level = 0;
			this.options = options;
			this.outWriter = outWriter;
			this.errorWriter = errorWriter;
			this.currentTestName = string.Empty;
			this.progress = !options.xmlConsole && !options.labels && !options.nodots;

			AppDomain.CurrentDomain.UnhandledException += 
				new UnhandledExceptionEventHandler(OnUnhandledException);
		}

		public bool HasExceptions
		{
			get { return unhandledExceptions.Count > 0; }
		}

		public void WriteExceptions()
		{
			Console.WriteLine();
			Console.WriteLine("Unhandled exceptions:");
			int index = 1;
			foreach( string msg in unhandledExceptions )
				Console.WriteLine( "{0}) {1}", index++, msg );
		}

		public void RunStarted(string name, int testCount)
		{
		}

		public void RunFinished(TestResult result)
		{
		}

		public void RunFinished(Exception exception)
		{
		}

		public void TestFinished(TestResult testResult)
		{
			if (testResult.Test.ClassName != null && !describes.Contains(testResult.Test.ClassName)) {
				Console.WriteLine ("<DESCRIBE::>" + testResult.Test.ClassName);
				describes.Add (testResult.Test.ClassName);
			}
			if (testResult.Test.MethodName != null) {
				Console.WriteLine ("<IT::>" + testResult.Test.MethodName);
			}

            switch( testResult.ResultState )
            {
				case ResultState.Failure:
				case ResultState.Cancelled:
					testRunCount++;
					failureCount++;
	    					
					if (progress)
						Console.WriteLine ("<FAILED::>" + FormatMessage (testResult.Message));
	    					
					messages.Add (string.Format ("{0}) {1} :", failureCount, testResult.Test.TestName.FullName));
					messages.Add (testResult.Message.Trim (Environment.NewLine.ToCharArray ()));

					string stackTrace = StackTraceFilter.Filter (testResult.StackTrace);
					if (stackTrace != null && stackTrace != string.Empty) {
						string[] trace = stackTrace.Split (System.Environment.NewLine.ToCharArray ());
						foreach (string s in trace) {
							if (s != string.Empty) {
								string link = Regex.Replace (s.Trim (), @".* in (.*):line (.*)", "$1($2)");
								messages.Add (string.Format ("at\n{0}", link));
							}
						}
					}
					if (testResult.ResultState == ResultState.Error) {
						Console.WriteLine ("<ERROR::>" + FormatMessage (stackTrace));
					} else if (stackTrace != null) {
						Console.WriteLine (FormatMessage (stackTrace));
					}
                    break;

				case ResultState.Inconclusive:
				case ResultState.Success:
                    if(testResult.Message == null)
                    {
                        Console.WriteLine("<PASSED::>Test Passed");
                    }
                    else
                    {
                        Console.WriteLine("<PASSED::>" + FormatMessage(testResult.Message));
                    }
                    testRunCount++;
                    break;

                case ResultState.Ignored:
                case ResultState.Skipped:
                case ResultState.NotRunnable:
    				testIgnoreCount++;
					
	    			if ( progress )
		    			Console.Write("N");
                    break;
			}

			currentTestName = string.Empty;
		}

		public void TestStarted(TestName testName)
		{
//			Console.WriteLine ("<IT::>" + FormatMessage (testName.Name));
		}

		public void SuiteStarted(TestName testName)
		{
			if ( level++ == 0 )
			{
				messages = new StringCollection();
				testRunCount = 0;
				testIgnoreCount = 0;
				failureCount = 0;
				Trace.WriteLine( "################################ UNIT TESTS ################################" );
				Trace.WriteLine( "Running tests in '" + testName.FullName + "'..." );
//				Console.WriteLine ("<DESCRIBE::>" + FormatMessage (testName.UniqueName));
			}
		}

		public void SuiteFinished(TestResult suiteResult) 
		{
			if ( --level == 0) 
			{
				Trace.WriteLine( "############################################################################" );

				if (messages.Count == 0) 
				{
					Trace.WriteLine( "##############                 S U C C E S S               #################" );
				}
				else 
				{
					Trace.WriteLine( "##############                F A I L U R E S              #################" );
						
					foreach ( string s in messages ) 
					{
						Trace.WriteLine(s);
					}
				}

				Trace.WriteLine( "############################################################################" );
				Trace.WriteLine( "Executed tests       : " + testRunCount );
				Trace.WriteLine( "Ignored tests        : " + testIgnoreCount );
				Trace.WriteLine( "Failed tests         : " + failureCount );
				Trace.WriteLine( "Unhandled exceptions : " + unhandledExceptions.Count);
				Trace.WriteLine( "Total time           : " + suiteResult.Time + " seconds" );
				Trace.WriteLine( "############################################################################");
			}
		}

		private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			if (e.ExceptionObject.GetType() != typeof(System.Threading.ThreadAbortException))
			{
				this.UnhandledException((Exception)e.ExceptionObject);
			}
		}


		public void UnhandledException( Exception exception )
		{
			// If we do labels, we already have a newline
			unhandledExceptions.Add(currentTestName + " : " + exception.ToString());
			//if (!options.labels) outWriter.WriteLine();
			string msg = string.Format("##### Unhandled Exception while running {0}", currentTestName);
			//outWriter.WriteLine(msg);
			//outWriter.WriteLine(exception.ToString());

			Console.WriteLine("<ERROR::>" + FormatMessage(msg + "\n" + exception.ToString()));
			Trace.WriteLine(exception.ToString());
		}

		public void TestOutput( TestOutput output)
		{
			switch ( output.Type )
			{
				case TestOutputType.Out:
					outWriter.Write( output.Text );
					break;
				case TestOutputType.Error:
					errorWriter.Write( output.Text );
					break;
			}
		}


		public override object InitializeLifetimeService()
		{
			return null;
		}
	}
}
