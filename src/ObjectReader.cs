#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace FastMember;

public class ObjectReader : DbDataReader
{
    private IEnumerator source;
    private readonly TypeAccessor accessor;
    private readonly string[] memberNames;
    private readonly Type[] effectiveTypes;
    private readonly BitArray allowNull;
    private object current;
    private bool active = true;

    public static ObjectReader Create<T>(IEnumerable<T> source, params string[] members)
    {
        return new ObjectReader(typeof(T), (IEnumerable)source, members);
    }

    public ObjectReader(Type type, IEnumerable source, params string[] members)
    {
        if (source == null)
            throw new ArgumentOutOfRangeException(nameof(source));
        bool flag1 = members == null || members.Length == 0;
        this.accessor = TypeAccessor.Create(type);
        if (this.accessor.GetMembersSupported)
        {
            List<Member> list = this.accessor.GetMembers().OrderBy<Member, int>((Func<Member, int>)(p => p.Ordinal)).ToList<Member>();
            if (flag1)
            {
                members = new string[list.Count];
                for (int index = 0; index < members.Length; ++index)
                    members[index] = list[index].Name;
            }
            this.allowNull = new BitArray(members.Length);
            this.effectiveTypes = new Type[members.Length];
            for (int index1 = 0; index1 < members.Length; ++index1)
            {
                Type type1 = (Type)null;
                bool flag2 = true;
                string member1 = members[index1];
                foreach (Member member2 in list)
                {
                    if (member2.Name == member1)
                    {
                        if (type1 == (Type)null)
                        {
                            Type type2 = member2.Type;
                            Type type3 = Nullable.GetUnderlyingType(type2);
                            if ((object)type3 == null)
                                type3 = type2;
                            type1 = type3;
                            flag2 = !type1.IsValueType || !(type1 == type2);
                        }
                        else
                        {
                            type1 = (Type)null;
                            break;
                        }
                    }
                }
                this.allowNull[index1] = flag2;
                Type[] effectiveTypes = this.effectiveTypes;
                int index2 = index1;
                Type type4 = type1;
                if ((object)type4 == null)
                    type4 = typeof(object);
                effectiveTypes[index2] = type4;
            }
        }
        else if (flag1)
            throw new InvalidOperationException("Member information is not available for this type; the required members must be specified explicitly");
        this.current = (object)null;
        this.memberNames = (string[])members.Clone();
        this.source = source.GetEnumerator();
    }

    public override int Depth => 0;

    public override DataTable GetSchemaTable()
    {
        DataTable schemaTable = new DataTable()
        {
            Columns = {
        {
          "ColumnOrdinal",
          typeof (int)
        },
        {
          "ColumnName",
          typeof (string)
        },
        {
          "DataType",
          typeof (Type)
        },
        {
          "ColumnSize",
          typeof (int)
        },
        {
          "AllowDBNull",
          typeof (bool)
        }
      }
        };
        object[] objArray = new object[5];
        for (int index = 0; index < this.memberNames.Length; ++index)
        {
            objArray[0] = (object)index;
            objArray[1] = (object)this.memberNames[index];
            objArray[2] = this.effectiveTypes == null ? (object)typeof(object) : (object)this.effectiveTypes[index];
            objArray[3] = (object)-1;
            objArray[4] = (object)(bool)(this.allowNull == null ? 1 : (this.allowNull[index] ? 1 : 0));
            schemaTable.Rows.Add(objArray);
        }
        return schemaTable;
    }

    public override void Close() => this.Shutdown();

    public override bool HasRows => this.active;

    public override bool NextResult()
    {
        this.active = false;
        return false;
    }

    public override bool Read()
    {
        if (this.active)
        {
            IEnumerator source = this.source;
            if (source != null && source.MoveNext())
            {
                this.current = source.Current;
                return true;
            }
            this.active = false;
        }
        this.current = (object)null;
        return false;
    }

    public override int RecordsAffected => 0;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        this.Shutdown();
    }

    private void Shutdown()
    {
        this.active = false;
        this.current = (object)null;
        IDisposable source = this.source as IDisposable;
        this.source = (IEnumerator)null;
        source?.Dispose();
    }

    public override int FieldCount => this.memberNames.Length;

    public override bool IsClosed => this.source == null;

    public override bool GetBoolean(int i) => (bool)this[i];

    public override byte GetByte(int i) => (byte)this[i];

    public override long GetBytes(
      int i,
      long fieldOffset,
      byte[] buffer,
      int bufferoffset,
      int length)
    {
        byte[] src = (byte[])this[i];
        int val2 = src.Length - (int)fieldOffset;
        if (val2 <= 0)
            return 0;
        int count = Math.Min(length, val2);
        Buffer.BlockCopy((Array)src, (int)fieldOffset, (Array)buffer, bufferoffset, count);
        return (long)count;
    }

    public override char GetChar(int i) => (char)this[i];

    public override long GetChars(
      int i,
      long fieldoffset,
      char[] buffer,
      int bufferoffset,
      int length)
    {
        string str = (string)this[i];
        int val2 = str.Length - (int)fieldoffset;
        if (val2 <= 0)
            return 0;
        int count = Math.Min(length, val2);
        str.CopyTo((int)fieldoffset, buffer, bufferoffset, count);
        return (long)count;
    }

    protected override DbDataReader GetDbDataReader(int i) => throw new NotSupportedException();

    public override string GetDataTypeName(int i)
    {
        return (this.effectiveTypes == null ? (MemberInfo)typeof(object) : (MemberInfo)this.effectiveTypes[i]).Name;
    }

    public override DateTime GetDateTime(int i) => (DateTime)this[i];

    public override Decimal GetDecimal(int i) => (Decimal)this[i];

    public override double GetDouble(int i) => (double)this[i];

    public override Type GetFieldType(int i)
    {
        return this.effectiveTypes != null ? this.effectiveTypes[i] : typeof(object);
    }

    public override float GetFloat(int i) => (float)this[i];

    public override Guid GetGuid(int i) => (Guid)this[i];

    public override short GetInt16(int i) => (short)this[i];

    public override int GetInt32(int i) => (int)this[i];

    public override long GetInt64(int i) => (long)this[i];

    public override string GetName(int i) => this.memberNames[i];

    public override int GetOrdinal(string name) => Array.IndexOf<string>(this.memberNames, name);

    public override string GetString(int i) => (string)this[i];

    public override object GetValue(int i) => this[i];

    public override IEnumerator GetEnumerator()
    {
        return (IEnumerator)new DbEnumerator((DbDataReader)this);
    }

    public override int GetValues(object[] values)
    {
        string[] memberNames = this.memberNames;
        object current = this.current;
        TypeAccessor accessor = this.accessor;
        int values1 = Math.Min(values.Length, memberNames.Length);
        for (int index = 0; index < values1; ++index)
            values[index] = accessor[current, memberNames[index]] ?? (object)DBNull.Value;
        return values1;
    }

    public override bool IsDBNull(int i) => this[i] is DBNull;

    public override object this[string name]
    {
        get => this.accessor[this.current, name] ?? (object)DBNull.Value;
    }

    public override object this[int i]
    {
        get => this.accessor[this.current, this.memberNames[i]] ?? (object)DBNull.Value;
    }
}
