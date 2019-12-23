using System;
using System.Threading.Tasks;
using Thrift.Transport;
using Thrift.Protocol;
using DroHub.Helpers.Thrift;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Net;
using System.Reflection;
using System.Threading;
namespace DroHub.Tests.TestInfrastructure
{
   class DroneDeviceHelper
    {
        static void hack()
        {
            // use reflection to remove IsRequestRestricted from headerInfo hash table
            Assembly a = typeof(WebHeaderCollection).Assembly;
            Console.WriteLine(a.GetType("System.Net.HeaderInfoTable", true, true) == null);
            foreach (FieldInfo f in a.GetType("System.Net.HeaderInfoTable").GetFields(BindingFlags.NonPublic | BindingFlags.Static))
            {
                Console.WriteLine(f.Name);
                if (f.Name == "s_headerHashTable")
                {
                    Console.WriteLine("Hey1");
                    Hashtable hashTable = f.GetValue(null) as Hashtable;
                    foreach (string sKey in hashTable.Keys)
                    {

                        object headerInfo = hashTable[sKey];
                        Console.WriteLine(String.Format("{0}: {1}", sKey, hashTable[sKey]));
                        foreach (FieldInfo g in a.GetType("System.Net.HeaderInfo").GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                        {

                            if (g.Name == "IsRequestRestricted")
                            {
                                bool b = (bool)g.GetValue(headerInfo);
                                if (b)
                                {
                                    g.SetValue(headerInfo, false);
                                    Console.WriteLine(sKey + "." + g.Name + " changed to false");
                                }

                            }
                        }

                    }
                }
            }
        }
        public delegate Task DroneTestDelegate();
        public static async Task mockDrone(DroHubFixture fixture, DroneRPC drone_rpc, string device_serial, DroneTestDelegate test_delegate)
        {
            var loggerFactory = new LoggerFactory().AddConsole().AddDebug(LogLevel.Trace);
            using (var ws_transport = new TWebSocketClient(fixture.ThriftUri, System.Net.WebSockets.WebSocketMessageType.Binary))
            using (var framed_transport = new TFramedTransport(ws_transport))
            using (var reverse_tunnel_transport = new TReverseTunnelServer(framed_transport, 1))
            {

                ws_transport.WebSocketOptions.SetRequestHeader("User-Agent", "AirborneProjets");
                ws_transport.WebSocketOptions.SetRequestHeader("Content-Type", "application/x-thrift");
                ws_transport.WebSocketOptions.SetRequestHeader("x-device-expected-serial", device_serial);

                var message_validator_factory = new TAMessageValidatorProtocol.Factory(new TJsonProtocol.Factory(),
                        TAMessageValidatorProtocol.ValidationModeEnum.KEEP_READING,
                        TAMessageValidatorProtocol.OperationModeEnum.SEQID_SLAVE);

                var processor = new Drone.AsyncProcessor(drone_rpc);
                var server_engine = new Thrift.Server.TSimpleAsyncServer(processor, reverse_tunnel_transport,
                        message_validator_factory, message_validator_factory, loggerFactory);
                await server_engine.ServeAsync(CancellationToken.None);
                await test_delegate();
            }
        }
    }
}
// Thrift.Server.TSimpleAsyncServer.TSimpleAsyncServer(Thrift.Processor.ITAsyncProcessor processor, TServerTransport serverTransport, TProtocolFactory inputProtocolFactory, TProtocolFactory outputProtocolFactory, ILoggerFactory loggerFactory, int clientWaitingDelay = 10)
