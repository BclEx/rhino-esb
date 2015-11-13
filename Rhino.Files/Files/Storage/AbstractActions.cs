using Common.Logging;
using Rhino.Files.Exceptions;
using Rhino.Files.Model;
using System;
using System.Collections.Generic;

namespace Rhino.Files.Storage
{
    public class AbstractActions : IDisposable
    {
        protected readonly Dictionary<string, QueueActions> _queuesByName = new Dictionary<string, QueueActions>();
        readonly string _database;

        public AbstractActions(string database)
        {
            _database = database;
        }

        public void Dispose()
        {
        }

        public QueueActions GetQueue(string queueName)
        {
            QueueActions actions;
            if (_queuesByName.TryGetValue(queueName, out actions))
                return actions;
            if (false)
                throw new QueueDoesNotExistsException(queueName);

            _queuesByName[queueName] = actions = new QueueActions(_database, queueName, GetSubqueues(queueName), this, i => AddToNumberOfMessagesIn(queueName, i));
            return actions;
        }

        private string[] GetSubqueues(string queueName)
        {
            var list = new List<string>();

            //Api.JetSetCurrentIndex(session, subqueues, "by_queue");
            //Api.MakeKey(session, subqueues, queueName, Encoding.Unicode, MakeKeyGrbit.NewKey);

            //if (Api.TrySeek(session, subqueues, SeekGrbit.SeekEQ) == false)
            //    return list.ToArray();

            //Api.MakeKey(session, subqueues, queueName, Encoding.Unicode, MakeKeyGrbit.NewKey);
            //try
            //{
            //    Api.JetSetIndexRange(session, subqueues, SetIndexRangeGrbit.RangeInclusive | SetIndexRangeGrbit.RangeUpperLimit);
            //}
            //catch (EsentErrorException e)
            //{
            //    if (e.Error != JET_err.NoCurrentRecord)
            //        throw;
            //    return list.ToArray();
            //}

            //do
            //{
            //    list.Add(Api.RetrieveColumnAsString(session, subqueues, ColumnsInformation.SubqueuesColumns["subqueue"]));
            //} while (Api.TryMoveNext(session, subqueues));


            return list.ToArray();
        }

        public void AddSubqueueTo(string queueName, string subQueue)
        {
            //try
            //{
            //    using (var update = new Update(session, subqueues, JET_prep.Insert))
            //    {
            //        Api.SetColumn(session, subqueues, ColumnsInformation.SubqueuesColumns["queue"], queueName, Encoding.Unicode);
            //        Api.SetColumn(session, subqueues, ColumnsInformation.SubqueuesColumns["subqueue"], subQueue, Encoding.Unicode);

            //        update.Save();
            //    }
            //}
            //catch (EsentErrorException e)
            //{
            //    if (e.Error != JET_err.KeyDuplicate)
            //        throw;
            //}
        }

        private void AddToNumberOfMessagesIn(string queueName, int count)
        {
            //Api.JetSetCurrentIndex(session, queues, "pk");
            //Api.MakeKey(session, queues, queueName, Encoding.Unicode, MakeKeyGrbit.NewKey);

            //if (Api.TrySeek(session, queues, SeekGrbit.SeekEQ) == false)
            //    return;

            //var bytes = BitConverter.GetBytes(count);
            //int actual;
            //Api.JetEscrowUpdate(session, queues, ColumnsInformation.QueuesColumns["number_of_messages"], bytes, bytes.Length, null, 0, out actual, EscrowUpdateGrbit.None);
        }

        public void Commit()
        {
        }
    }
}

