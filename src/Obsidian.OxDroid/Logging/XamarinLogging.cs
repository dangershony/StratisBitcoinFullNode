using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Microsoft.Extensions.Logging;

namespace Obsidian.DroidD.Logging
{
    public class XamarinLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, XamarinLogger> loggers =
            new ConcurrentDictionary<string, XamarinLogger>();

        public ILogger CreateLogger(string categoryName)
        {
            if (categoryName == null)
                categoryName = "No category";

            return this.loggers.GetOrAdd(categoryName, name => new XamarinLogger(name));
        }

        public void Dispose()
        {
            this.loggers.Clear();
        }
    }

    public class XamarinLogger : ILogger
    {
        public static event EventHandler EntryAdded;

        public static readonly ConcurrentQueue<XamarinLoggerEventArgs> Queue =
            new ConcurrentQueue<XamarinLoggerEventArgs>();

        private readonly string name;

        public XamarinLogger(string name)
        {
            this.name = name;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel != LogLevel.Trace)
                return true;
            return false;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Trace)
                return;
            var text = $"{logLevel.ToString()} - {eventId.Id} - {this.name} - {formatter(state, exception)}";

            var ea = new XamarinLoggerEventArgs(logLevel, this.name, text);
            Queue.Enqueue(ea);
            while (Queue.Count > 1000)
            {
                Queue.TryDequeue(out var discarded);
            }

            //EntryAdded?.Invoke(typeof(XamarinLogger), ea);
        }

        public class XamarinLoggerEventArgs : EventArgs
        {
            public readonly LogLevel LogLevel;
            public readonly string Name;
            public readonly string Text;

            public XamarinLoggerEventArgs(LogLevel logLevel, string name, string text)
            {
                LogLevel = logLevel;
                Name = name;
                Text = text;
            }
        }
    }
}
