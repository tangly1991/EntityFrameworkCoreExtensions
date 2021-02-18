using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Ame.EntityFrameworkCore.Extensions
{
    public static class QueryableJoinExtended
    {
        private static Expression<Func<TOuter, TInner, TResult>> CastSMLambda<TOuter, TInner, TResult>(LambdaExpression ex, TOuter _1, TInner _2, TResult _3) => (Expression<Func<TOuter, TInner, TResult>>)ex;

        public static IQueryable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(
            this IQueryable<TOuter> outer,
            IQueryable<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TResult>> resultSelector
        )
        {
            var gjResTemplate = new { outer = default(TOuter), innerj = default(IEnumerable<TInner>) };

            // oij = new { outer, innerj }
            var oijParm = Expression.Parameter(gjResTemplate.GetType(), "oij");

            // i
            var iParm = Expression.Parameter(typeof(TInner), "i");

            // oij.outer
            var oijOuter = Expression.PropertyOrField(oijParm, "outer");

            // (oij,i) => resExpr(oij.outer, i)
            var selectResExpr = CastSMLambda(Expression.Lambda(Expression.Invoke(resultSelector, oijOuter, iParm), oijParm, iParm), gjResTemplate, default(TInner), default(TResult));

            return outer.GroupJoin(inner, outerKeySelector, innerKeySelector, (outer, innerj) => new { outer, innerj })
                .SelectMany(r => r.innerj.DefaultIfEmpty(), selectResExpr);
        }
    }
}
