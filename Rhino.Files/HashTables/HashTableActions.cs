using System;
using System.Collections.Generic;

namespace Rhino.HashTables
{
    public class HashTableActions : IDisposable
    {
        string _database;

        public HashTableActions(string database)
        {
            _database = database;
        }

        public void Dispose()
        {
        }

        public int AddItem(AddItemRequest request)
        {
            return 0;
            //int num;
            //byte[] bookmark = new byte[0x100];
            //using (Update update = new Update((JET_SESID)this.Session, (JET_TABLEID)this.list, JET_prep.Insert))
            //{
            //    Api.SetColumn((JET_SESID)this.session, (JET_TABLEID)this.list, this.listColumns["key"], request.Key, Encoding.Unicode);
            //    Api.SetColumn((JET_SESID)this.session, (JET_TABLEID)this.list, this.listColumns["data"], request.Data);
            //    update.Save(bookmark, bookmark.Length, out num);
            //}
            //Api.JetGotoBookmark((JET_SESID)this.session, (JET_TABLEID)this.list, bookmark, num);
            //return Api.RetrieveColumnAsInt32((JET_SESID)this.session, (JET_TABLEID)this.list, this.listColumns["id"]).Value;
        }

        public void RemoveItem(RemoveItemRequest request)
        {
            //Api.JetSetCurrentIndex((JET_SESID)this.session, (JET_TABLEID)this.list, "pk");
            //Api.MakeKey((JET_SESID)this.session, (JET_TABLEID)this.list, request.Key, Encoding.Unicode, MakeKeyGrbit.NewKey);
            //Api.MakeKey((JET_SESID)this.session, (JET_TABLEID)this.list, request.Id, MakeKeyGrbit.None);
            //if (Api.TrySeek((JET_SESID)this.session, (JET_TABLEID)this.list, SeekGrbit.SeekEQ))
            //{
            //    Api.JetDelete((JET_SESID)this.session, (JET_TABLEID)this.list);
            //}
        }

        public KeyValuePair<int, byte[]>[] GetItems(GetItemsRequest request)
        {
            //Api.JetSetCurrentIndex((JET_SESID)this.session, (JET_TABLEID)this.list, "by_key");
            //Api.MakeKey((JET_SESID)this.session, (JET_TABLEID)this.list, request.Key, Encoding.Unicode, MakeKeyGrbit.NewKey);
            //if (!Api.TrySeek((JET_SESID)this.session, (JET_TABLEID)this.list, SeekGrbit.SeekEQ))
            //{
            return new KeyValuePair<int, byte[]>[0];
            //}
            //Api.MakeKey((JET_SESID)this.session, (JET_TABLEID)this.list, request.Key, Encoding.Unicode, MakeKeyGrbit.NewKey);
            //Api.JetSetIndexRange((JET_SESID)this.session, (JET_TABLEID)this.list, SetIndexRangeGrbit.RangeUpperLimit | SetIndexRangeGrbit.RangeInclusive);
            //List<KeyValuePair<int, byte[]>> list = new List<KeyValuePair<int, byte[]>>();
            //do
            //{
            //    int? nullable = Api.RetrieveColumnAsInt32((JET_SESID)this.session, (JET_TABLEID)this.list, this.listColumns["id"]);
            //    byte[] buffer = Api.RetrieveColumn((JET_SESID)this.session, (JET_TABLEID)this.list, this.listColumns["data"]);
            //    list.Add(new KeyValuePair<int, byte[]>(nullable.Value, buffer));
            //}
            //while (Api.TryMoveNext((JET_SESID)this.Session, (JET_TABLEID)this.list));
            //return list.ToArray();
        }

        public void Commit()
        {
            //this.CleanExpiredValues();
            //this.transaction.Commit(CommitTransactionGrbit.None);
            //foreach (Action action in this.commitSyncronization)
            //{
            //    action();
            //}
        }
    }
}