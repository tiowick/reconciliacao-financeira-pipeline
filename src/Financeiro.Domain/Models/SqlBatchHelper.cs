using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Financeiro.Domain.Models
{
    public static class SqlBatchHelper
    {
        public static DataTable ToTable<T>(IEnumerable<T> items)
        {
            var dt = new DataTable();
            var props = typeof(T).GetProperties();

            foreach (var p in props)
                dt.Columns.Add(p.Name, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType);

            foreach (var item in items)
                dt.Rows.Add(props.Select(p => p.GetValue(item) ?? DBNull.Value).ToArray());

            return dt;
        }
    }
}
