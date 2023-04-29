using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using NUnit.Framework;

namespace Test.AveragingPaper
{
    [TestFixture]
    public static class TestDB
    {
        [Test]
        public static void TESTNAME()
        {
            var connection = new SQLiteConnection($"Data Source={DatabaseConstants.DatabasePath}");
            connection.Open();


        }
    }


    internal static class DatabaseConstants
    {
        public static string DatabasePath = @"B:\Users\Nic\ScanAveraging\Averaging.sql";
    }
    public class DbColumnAttribute : Attribute
    {
        /// <summary>
        /// Set true if implicit conversion is required.
        /// </summary>
        public bool Convert { get; set; }
        /// <summary>
        /// Set true if the property is primary key in the table
        /// </summary>
        public bool IsPrimary { get; set; }
        /// <summary>
        /// denotes if the field is an identity type or not.
        /// </summary>
        public bool IsIdentity { get; set; }
    }

    public interface IFilter<T> where T : class, new()
    {
        string EntityName { get; }
        string Query { get; }

        void Add(Expression<Func<T, object>> memberExpression, object memberValue);
    }

    public class Filter<T> : IFilter<T> where T : class, new()
    {

        public Filter()
        {
            _Query = new StringBuilder();
            EntityName = typeof(T).Name;
        }

        public void Add(Expression<Func<T, object>> memberExpression, object memberValue)
        {

            if (_Query.ToString() != string.Empty)
                _Query.Append(" AND ");

            _Query.Append(string.Format(" [{0}] = {1}", NameOf(memberExpression), memberValue == null ? "NULL" : string.Format("'{0}'", memberValue)));
        }

        public string EntityName { get; private set; }

        private readonly StringBuilder _Query;

        public string Query
        {
            get
            {
                return string.Format("SELECT * FROM [{0}] {1} {2};"
                    , EntityName
                    , _Query.ToString() == string.Empty ? string.Empty : "WHERE"
                    , _Query.ToString());
            }
        }

        private string NameOf(Expression<Func<T, object>> exp)
        {
            MemberExpression body = exp.Body as MemberExpression;

            if (body == null)
            {
                UnaryExpression ubody = (UnaryExpression)exp.Body;
                body = ubody.Operand as MemberExpression;
            }

            return body.Member.Name;
        }
    }
    public class EntityMapper
    {
        // Complete
        public IList<T> Map<T>(SQLiteDataReader reader)
            where T : class, new()
        {
            IList<T> collection = new List<T>();
            while (reader.Read())
            {
                T obj = new T();
                foreach (PropertyInfo i in obj.GetType().GetProperties()
                             .Where(p => p.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(DbColumnAttribute)) != null).ToList())
                {

                    try
                    {
                        var ca = i.GetCustomAttribute(typeof(DbColumnAttribute));

                        if (ca != null)
                        {
                            if (((DbColumnAttribute)ca).Convert == true)
                            {
                                if (reader[i.Name] != DBNull.Value)
                                    i.SetValue(obj, Convert.ChangeType(reader[i.Name], i.PropertyType));
                            }
                            else
                            {
                                if (reader[i.Name] != DBNull.Value)
                                    i.SetValue(obj, reader[i.Name]);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        Console.WriteLine(ex.Message);
                        Console.ReadLine();
#endif

#if !DEBUG
                    throw ex;
#endif
                    }
                }
                collection.Add(obj);
            }
            return collection;
        }
    }

   
}
