using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;

namespace Jobtech.OpenPlatforms.GigDataApi.Common.RavenDB
{
    public class DocumentStoreHolder
    {
        private static Lazy<IDocumentStore> _store = new Lazy<IDocumentStore>(CreateStore);
        public static bool IsDevelopment { get; set; }
        public static ILogger Logger { get; set; }
        public static string[] Urls { get; set; }
        public static string CertificateThumbprint { get; set; }
        public static string DatabaseName { get; set; }
        public static Type TypeInAssemblyContainingIndexesToCreate { get; set; }

        public static IDocumentStore Store => _store.Value;

        private static IDocumentStore CreateStore()
        {
            IDocumentStore store = null;

            if (IsDevelopment)
            {
                Logger?.LogTrace("Will init DocumentStore for development");
                try
                {
                    store = new DocumentStore()
                    {
                        Urls = Urls,
                        Database =  DatabaseName
                    }.Initialize();
                }
                catch (Exception e)
                {
                    Logger?.LogError(e, "Error initializing DocumentStore");
                    throw;
                }
            }
            else
            {
                Logger?.LogTrace("Will init DocumentStore for non development");

                try
                {
                    var cert = GetCert();

                    store = new DocumentStore()
                    {
                        Urls = Urls,
                        Database = DatabaseName,
                        Certificate = cert
                    }.Initialize();
                }
                catch (Exception e)
                {
                    Logger?.LogError(e, "Error initializing DocumentStore");
                    throw;
                }
            }

            if (TypeInAssemblyContainingIndexesToCreate != null)
            {
                Logger?.LogTrace($"Will create indices defined in assembly {TypeInAssemblyContainingIndexesToCreate.Name}");
                IndexCreation.CreateIndexes(TypeInAssemblyContainingIndexesToCreate.Assembly, store);
            }
            

            return store;
        }

        private static X509Certificate2 GetCert()
        {
            var bytes = System.IO.File.ReadAllBytes($"/app/certs/tls.cer");
            var cert = new X509Certificate2();
            cert.Import(bytes);

            return cert;
        }
    }
}
