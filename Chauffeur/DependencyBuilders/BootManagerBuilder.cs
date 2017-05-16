using Chauffeur.Services;
using System.IO;
using Umbraco.Core;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Services;
using System;
using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.Profiling;

namespace Chauffeur.DependencyBuilders
{
    class BootManagerBuilder : IBuildDependencies
    {
        public void Build(IContainer container)
        {
            var application = new ChauffeurUmbracoApplication();
            application.Start();

            var context = ApplicationContext.Current;

            var writer = container.Resolve<TextWriter>();
            var logger = new TextWriterLogger(writer);
            
            container.Register<Umbraco.Core.Logging.ILogger>(() => logger);
            container.Register(() => context.DatabaseContext);
            container.Register(() => context.DatabaseContext.Database).As<Database>();

            container.Register(() => context);
            container.Register(() => context.DatabaseContext.SqlSyntax);
            container.Register(() => new DatabaseSchemaHelper(context.DatabaseContext.Database, logger, context.DatabaseContext.SqlSyntax));

            var services = context.Services;
            container.Register(() => services.ContentService);
            container.Register(() => services.ContentTypeService);
            container.Register(() => services.DataTypeService);
            container.Register(() => services.FileService);
            container.Register(() => services.MediaService);
            container.Register(() => services.MacroService);
            container.Register(() => services.MemberGroupService);
            container.Register(() => services.MemberService);
            container.Register(() => services.MemberTypeService);
            container.Register(() => new OverridingPackagingService(services.PackagingService, services.MacroService)).As<IPackagingService>();
            container.Register(() => services.UserService);
        }

        class ChauffeurBootManager : CoreBootManager
        {
            static bool initialized = false;

            public ChauffeurBootManager(UmbracoApplicationBase application)
                : base(application)
            {
            }

            public override IBootManager Initialize()
            {
                base.Initialize();
                initialized = true;
                return this;
            }
            protected override void InitializeResolvers()
            {
                if (!initialized)
                {
                    base.InitializeResolvers();
                }
            }

            protected override void InitializeProfilerResolver()
            {
                var resolver = typeof(ResolverBase<>).MakeGenericType(typeof(ResolverBase<>).Assembly.GetType("Umbraco.Core.Profiling.ProfilerResolver"));
                var isInit = (bool)resolver.GetProperty("HasCurrent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).GetValue(null, null);

                if (!isInit)
                {
                    base.InitializeProfilerResolver();
                }
            }
            protected override void InitializeLoggerResolver()
            {
                if (!ResolverBase<LoggerResolver>.HasCurrent)
                {
                    base.InitializeLoggerResolver();
                }
            }

            protected override void InitializeApplicationEventsResolver()
            {
                if (!ResolverBase<ApplicationEventsResolver>.HasCurrent)
                {
                    base.InitializeApplicationEventsResolver();
                }
            }
        }

        class ChauffeurUmbracoApplication : UmbracoApplicationBase
        {
            protected override IBootManager GetBootManager()
            {
                return new ChauffeurBootManager(this);
            }

            public void Start()
            {
                GetBootManager()
                    .Initialize();
            }
        }

        class DummyProfiler : IProfiler
        {
            class DummyDispose : IDisposable
            {
                public void Dispose()
                {
                }
            }

            public string Render()
            {
                return string.Empty;
            }

            public void Start()
            {
            }

            public IDisposable Step(string name)
            {
                return new DummyDispose();
            }

            public void Stop(bool discardResults = false)
            {
            }
        }

        class TextWriterLogger : LambdaLogger
        {
            public TextWriterLogger(TextWriter writer) : base(writer.WriteLine) { }
        }

       class LambdaLogger : Umbraco.Core.Logging.ILogger
            {
                private Action<string> callback;

                public LambdaLogger(Action<string> callback)
                {
                    this.callback = callback;
                }


            public void Debug(Type callingType, Func<string> generateMessage)
            {
                    callback($"DEBUG {callingType} {generateMessage()}");
            }

            public void Debug(Type type, string generateMessageFormat, params Func<object>[] formatItems)
            {
                Debug(type, () => string.Format(generateMessageFormat, args: formatItems?.Select(x => x == null? null : x()).ToArray()));
            }

            public void Error(Type callingType, string message, Exception exception)
            {
                    callback($"ERROR {callingType} {message} {exception?.ToString()}");
            }

            public void Info(Type callingType, Func<string> generateMessage)
            {
                    callback($"INFO {callingType} {generateMessage()}");
            }

            public void Info(Type type, string generateMessageFormat, params Func<object>[] formatItems)
            {
                Info(type, () => string.Format(generateMessageFormat, args: formatItems?.Select(x => x == null ? null : x()).ToArray()));
            }

            public void Warn(Type callingType, string message, params Func<object>[] formatItems)
            {
                WarnWithException(callingType, message, e: null, formatItems: formatItems);                
            }

            public void WarnWithException(Type callingType, string message, Exception e, params Func<object>[] formatItems)
            {
                    callback($"WARN {callingType} {string.Format(message, args: formatItems?.Select(x => x == null ? null : x()).ToArray())} {e?.ToString()}");
            }
        }

    }
}
