using System;
using System.Collections.Generic;
using System.Reflection;
using TeamExplorer.Common;

namespace GitFlowVS.Extension
{
	public static class Logger
	{
		// No ApplicationInsights types referenced at class level.
		// This prevents TypeLoadException if the assembly cannot be loaded.
		private static Action<string, IDictionary<string, string>> _trackEvent;
		private static Action<string, double> _trackMetric;
		private static Action<Exception> _trackException;

		static Logger()
		{
			try
			{
				// ApplicationInsights types are only referenced inside this method.
				// If AI cannot be loaded, the TypeLoadException is thrown here at the
				// call site and caught, so Logger itself initializes successfully.
				InitializeTelemetry();
			}
			catch
			{
				// Telemetry unavailable; all public methods become silent no-ops.
			}
		}

		private static void InitializeTelemetry()
		{
			var config = new Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration
			{
				ConnectionString = "InstrumentationKey=d4f789f2-d29e-4b15-9635-440018ad3f2d"
			};
			var client = new Microsoft.ApplicationInsights.TelemetryClient(config);

			try { client.Context.GlobalProperties["VisualStudioVersion"] = VSVersion.FullVersion.ToString(); } catch { }
			try { client.Context.Component.Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(); } catch { }
			client.Context.Session.Id = Guid.NewGuid().ToString();
			try { client.Context.User.Id = UserSettings.UserId; } catch { client.Context.User.Id = Guid.NewGuid().ToString(); }

			_trackEvent = (name, props) =>
			{
				if (props != null)
					client.TrackEvent(name, props);
				else
					client.TrackEvent(name);
			};
			_trackMetric = (name, value) => client.TrackMetric(name, value);
			_trackException = ex => client.TrackException(ex);
		}

		public static void PageView(string page)
		{
			_trackEvent?.Invoke("PageView", new Dictionary<string, string> { { "Page", page } });
		}

		public static void Event(string eventName, IDictionary<string, string> properties = null)
		{
			_trackEvent?.Invoke(eventName, properties);
		}

		public static void Metric(string name, double value)
		{
			_trackMetric?.Invoke(name, value);
		}

		public static void Exception(Exception ex)
		{
			_trackException?.Invoke(ex);
		}
	}
}
