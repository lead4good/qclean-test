/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Linq;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Logging;
using QuantConnect.Packets;
using QuantConnect.Util;
using QuantConnect.Notifications;
using QuantConnect.Data;
using QuantConnect.Algorithm;
using Newtonsoft.Json;

namespace QuantConnect.Lean.TestProject
{

    public class Program
    {

	private static BacktestingResultHandler _resultsHandler;

	static void Main(string[] args)
        {
		Config.Set("algorithm-location", "QuantConnect.Lean.TestProject.Algorithm.dll");
		Config.Set("algorithm-type-name", "QuantConnect.Lean.TestProject.Algorithm.BasicTemplateAlgorithm");

		LaunchLean();

		//var lines = _resultsHandler.FinalStatistics.Select(kvp => kvp.Key + ": " + kvp.Value.ToString());
		//Log.Trace(string.Join(Environment.NewLine, lines));

	/*	var sharpe = -10m;
		var ratio = _resultsHandler.FinalStatistics["Sharpe Ratio"];
		Decimal.TryParse(ratio, out sharpe);
		var compound = _resultsHandler.FinalStatistics["Compounding Annual Return"];
		decimal parsed;
		Decimal.TryParse(compound.Trim('%'), out parsed);

		bool includeNegativeReturn = (bool)AppDomain.CurrentDomain.GetData("IncludeNegativeReturn");
		if (!includeNegativeReturn)
		{
		sharpe = System.Math.Max(sharpe <= 0 || parsed < 0 ? -10 : sharpe, -10);
		}
		else
		{
		sharpe = System.Math.Max(sharpe, -10);
		}*/
	}

	private static void LaunchLean()
        {
/*            Config.Set("environment", "backtesting");
            string algorithm = (string)AppDomain.CurrentDomain.GetData("AlgorithmTypeName");
            string path = (string)AppDomain.CurrentDomain.GetData("AlgorithmLocation");

            Config.Set("algorithm-type-name", algorithm);
            if (!string.IsNullOrEmpty(path))
            {
                Config.Set("algorithm-location", path);
            }
*/
//	    Config.Set("result-handler", "BacktestingResultHandler");
	    Config.Set("messaging-handler", "QuantConnect.Lean.TestProject.Messaging");
	    

            var systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
            systemHandlers.Initialize();

            Log.LogHandler = Composer.Instance.GetExportedValueByTypeName<ILogHandler>(Config.Get("log-handler", "CompositeLogHandler"));
            //Log.DebuggingEnabled = false;
            //Log.DebuggingLevel = 1;

            LeanEngineAlgorithmHandlers leanEngineAlgorithmHandlers;
            try
            {
                leanEngineAlgorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance);
                _resultsHandler = (BacktestingResultHandler)leanEngineAlgorithmHandlers.Results;
            }
            catch (CompositionException compositionException)
            {
                Log.Error("Engine.Main(): Failed to load library: " + compositionException);
                throw;
            }
            string algorithmPath;
            AlgorithmNodePacket job = systemHandlers.JobQueue.NextJob(out algorithmPath);
            try
            {
                var _engine = new QuantConnect.Lean.Engine.Engine(systemHandlers, leanEngineAlgorithmHandlers, Config.GetBool("live-mode"));
                _engine.Run(job, algorithmPath);
            }
            finally
            {
                Log.Trace("Engine.Main(): Packet removed from queue: " + job.AlgorithmId);

                // clean up resources
                systemHandlers.Dispose();
                leanEngineAlgorithmHandlers.Dispose();
                Log.LogHandler.Dispose();
            }
        }
    }

    /// <summary>
    /// Local/desktop implementation of messaging system for Lean Engine.
    /// </summary>
    public class Messaging : IMessagingHandler
    {
        // used to aid in generating regression tests via Cosole.WriteLine(...)
        private static readonly TextWriter Console = System.Console.Out;

        private AlgorithmNodePacket _job;

        /// <summary>
        /// This implementation ignores the <seealso cref="HasSubscribers"/> flag and
        /// instead will always write to the log.
        /// </summary>
        public bool HasSubscribers
        {
            get; 
            set;
        }

        /// <summary>
        /// Initialize the messaging system
        /// </summary>
        public void Initialize()
        {
            //
        }

        /// <summary>
        /// Set the messaging channel
        /// </summary>
        public void SetAuthentication(AlgorithmNodePacket job)
        {
            _job = job;
        }

        /// <summary>
        /// Send a generic base packet without processing
        /// </summary>
        public void Send(Packet packet)
        {
            switch (packet.Type)
            {
                case PacketType.Debug:
                    var debug = (DebugPacket) packet;
                    Log.Trace("Debug: " + debug.Message);
                    break;

                case PacketType.Log:
                    var log = (LogPacket) packet;
                    Log.Trace("Log: " + log.Message);
                    break;

                case PacketType.RuntimeError:
                    var runtime = (RuntimeErrorPacket) packet;
                    var rstack = (!string.IsNullOrEmpty(runtime.StackTrace) ? (Environment.NewLine + " " + runtime.StackTrace) : string.Empty);
                    Log.Error(runtime.Message + rstack);
                    break;

                case PacketType.HandledError:
                    var handled = (HandledErrorPacket) packet;
                    var hstack = (!string.IsNullOrEmpty(handled.StackTrace) ? (Environment.NewLine + " " + handled.StackTrace) : string.Empty);
                    Log.Error(handled.Message + hstack);
                    break;

                case PacketType.BacktestResult:
                    var result = (BacktestResultPacket) packet;

                    if (result.Progress == 1)
                    {
                        // uncomment these code traces to help write regression tests
                        //Console.WriteLine("new Dictionary<string, string>");
                        //Console.WriteLine("\t\t\t{");
                        foreach (var pair in result.Results.Statistics)
                        {
                            Log.Trace("STATISTICS:: " + pair.Key + " " + pair.Value);
                            //Console.WriteLine("\t\t\t\t{{\"{0}\",\"{1}\"}},", pair.Key, pair.Value);
                        }
                        //Console.WriteLine("\t\t\t};");

                        //foreach (var pair in statisticsResults.RollingPerformances)
                        //{
                        //    Log.Trace("ROLLINGSTATS:: " + pair.Key + " SharpeRatio: " + Math.Round(pair.Value.PortfolioStatistics.SharpeRatio, 3));
                        //}
			var serialized = JsonConvert.SerializeObject(result.Results,Formatting.Indented);
			Log.Trace(serialized);
                    }
                    break;
            }


            if (QuantConnect.Messaging.StreamingApi.IsEnabled)
            {
                QuantConnect.Messaging.StreamingApi.Transmit(_job.UserId, _job.Channel, packet);
            }
        }

        /// <summary>
        /// Send any notification with a base type of Notification.
        /// </summary>
        public void SendNotification(Notification notification)
        {
            var type = notification.GetType();
            if (type == typeof (NotificationEmail)
             || type == typeof (NotificationWeb)
             || type == typeof (NotificationSms))
            {
                Log.Error("Messaging.SendNotification(): Send not implemented for notification of type: " + type.Name);
                return;
            }
            notification.Send();
        }
    }
}
