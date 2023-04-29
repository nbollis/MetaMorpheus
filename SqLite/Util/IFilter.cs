using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SqLite
{
    public interface IFilter<T> where T : class, new()
    {
        string EntityName { get; }
        string Query { get; }

        void Add(Expression<Func<T, object>> memberExpression, object memberValue);
    }
}
