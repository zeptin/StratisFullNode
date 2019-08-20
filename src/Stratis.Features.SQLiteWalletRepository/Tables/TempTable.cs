﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class TempRow
    {
    }

    internal class TempTable : List<TempRow>
    {
        internal TempTable(Type rowType)
        {
            this.RowType = rowType;
        }

        internal static TempTable Create<T>() where T : TempRow
        {
            return new TempTable(typeof(T));
        }

        internal Type RowType { get; set; }

        private static PropertyInfo[] GetProperties(Type objType)
        {
            return objType.GetProperties().Where(p => p.SetMethod != null).ToArray();
        }

        private static string ColumnType(PropertyInfo info)
        {
            string type = "TEXT";

            if (info.PropertyType == typeof(int))
                return "INT";

            if (info.PropertyType == typeof(decimal))
                return "DECIMAL";

            return type;
        }

        protected string ObjectColumns(bool includeType = false)
        {
            var props = GetProperties(this.RowType);

            if (!includeType)
                return $"({string.Join(",", props.Select(info => info.Name))})";

            return $"({string.Join(",", props.Select(info => $"{info.Name} {ColumnType(info)}"))})";
        }

        internal IEnumerable<string> CreateScript()
        {
            yield return $"DROP TABLE IF EXISTS temp.{this.RowType.Name}";
            yield return $"CREATE TABLE temp.{this.RowType.Name} {ObjectColumns(true)};";

            var props = GetProperties(this.RowType);

            if (this.Count > 0)
                yield return $"INSERT INTO temp.{this.RowType.Name} {ObjectColumns()} VALUES {string.Join(Environment.NewLine + ",", this.Select(obj => ObjectRow(props, obj)))};";
        }

        internal static string ObjectRow(PropertyInfo[] props, object obj)
        {
            var res = props.Select(p => p.GetValue(obj)).Select(prop => (prop.GetType() == typeof(string)) ? $"'{prop}'" : prop);
            var arr = string.Join(",", res);
            return $"({arr})";
        }
    }
}