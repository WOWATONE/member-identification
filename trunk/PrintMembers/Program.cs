using System;
using System.Linq;

using DevExpress.Xpo;
using DevExpress.Xpo.DB;

using PrintMembers.DatabaseCode;

using RfidBus.Primitives.Messages.Printers;
using RfidBus.Primitives.Messages.Printers.Elements;
using RfidBus.Primitives.Network;
using RfidBus.Serializers;

using RfidCenter.Basic;

namespace PrintMembers
{
    internal class Program
    {
        private static void Main()
        {
            try
            {
                ConnectionHelper.Connect(AutoCreateOption.DatabaseAndSchema);

                var client = new RfidBusClient(Properties.Settings.Default.BusHost,
                                               Properties.Settings.Default.BusPort,
                                               new PbSerializer());

                if (!client.Authorize(Properties.Settings.Default.BusLogin, Properties.Settings.Default.BusPassword))
                    throw new BaseException(RfidErrorCode.InvalidLoginAndPassword);

                var printerGuid = Guid.Parse(Properties.Settings.Default.RfidPrinter);

                const int startFrom = 0x00;
                var startBytes = BitConverter.GetBytes(startFrom);

                // первые 6 байт случайны - взяты из сгенерированного нового Guid :D
                var epc = new byte[] {0xF5, 0x55, 0xB4, 0xBF, 0xF7, 0x1E, 0x00, 0x00, startBytes[3], startBytes[2], startBytes[1], startBytes[0]};

                using (var session = new Session())
                {
                    var table = new XPCollection<Persona>(session);

                    var users = (from user in table
                                 where string.IsNullOrWhiteSpace(user.Tid)
                                 select user).ToArray();

                    foreach (var user in users)
                    {
                        var idBytes = BitConverter.GetBytes(user.Id).Reverse().ToArray();
                        Array.Copy(idBytes, 0, epc, 8, 4);

                        user.Tid = BaseTools.GetStringFromBinary(epc);
                        user.SawTimes = 0;

                        var initialsElement = new TextElement(10, 7, 80, 6, user.Initials, "0");
                        var occupationElement = new TextElement(10, 13, 80, 6, user.Occupation, "0");
                        var epcElement = new TextElement(10, 19, 80, 6, user.Tid, "0");

                        var writeEpcElement = new WriteDataLabelElement(epc, retries: 1);

                        var label = new RfidPrintLabel();
                        label.Elements.Add(initialsElement);
                        label.Elements.Add(occupationElement);
                        label.Elements.Add(epcElement);
                        label.Elements.Add(writeEpcElement);

                        client.SendRequest(new EnqueuePrintLabelTask(printerGuid, label));
                    }

                    session.Save(table);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} : {1}", ex.GetType().Name, ex.Message);
            }
            finally
            {
                Console.ReadLine();
            }
        }
    }
}