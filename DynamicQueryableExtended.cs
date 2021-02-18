using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Ame.EntityFrameworkCore.Extensions
{
    /// <summary>
    /// 关联表类
    /// </summary>
    public class IncludeTable
    {
        public string Name { get; set; }

        public PropertyInfo PropertyInfo { get; set; }

        public Type PropertyType { get; set; }

        public List<IncludeTable> Child { get; set; } = new List<IncludeTable>();
    }

    /// <summary>
    /// 字段信息类
    /// </summary>
    public class TableField
    {
        public string Key { get; set; }

        // 模型属性名
        public string Name { get; set; }

        // 模型属性信息，非模型关联属性为null
        public PropertyInfo PropertyInfo { get; set; }

        public Type PropertyType { get; set; }

        // 是否为集合
        public bool IsCollection { get; set; }

        // 字段类型为模型时，此集合表示需要显示的子表字段
        public List<TableField> Child { get; set; } = new List<TableField>();
    }

    public static class DynamicQueryableExtended
    {
        private static readonly List<IncludeTable> _includeTables = new List<IncludeTable>();
        private static readonly List<TableField> _tableFields = new List<TableField>();
        private static readonly List<TableField> _whereFields = new List<TableField>();

        /// <summary>
        /// The parameterizable types to Tuple mapping
        /// </summary>
        private readonly static Dictionary<Type, Type> ParameterizableTypes = new Dictionary<Type, Type>
        {
            {typeof(bool), typeof(Tuple<bool>)},
            {typeof(byte), typeof(Tuple<byte>)},
            {typeof(DateTimeOffset), typeof(Tuple<DateTimeOffset>)},
            {typeof(decimal), typeof(Tuple<decimal>)},
            {typeof(double), typeof(Tuple<double>)},
            {typeof(float), typeof(Tuple<float>)},
            {typeof(Guid), typeof(Tuple<Guid>)},
            {typeof(short), typeof(Tuple<short>)},
            {typeof(int), typeof(Tuple<int>)},
            {typeof(long), typeof(Tuple<long>)},
            {typeof(sbyte), typeof(Tuple<sbyte>)},
            {typeof(bool?), typeof(Tuple<bool?>)},
            {typeof(byte?), typeof(Tuple<byte?>)},
            {typeof(DateTimeOffset?), typeof(Tuple<DateTimeOffset?>)},
            {typeof(decimal?), typeof(Tuple<decimal?>)},
            {typeof(double?), typeof(Tuple<double?>)},
            {typeof(float?), typeof(Tuple<float?>)},
            {typeof(Guid?), typeof(Tuple<Guid?>)},
            {typeof(short?), typeof(Tuple<short?>)},
            {typeof(int?), typeof(Tuple<int?>)},
            {typeof(long?), typeof(Tuple<long?>)},
            {typeof(sbyte?), typeof(Tuple<sbyte?>)},
            {typeof(string), typeof(Tuple<string>)}
        };


        /// <summary>
        /// 模型类命名空间
        /// </summary>
        public static string ModelNamespace;

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="conditions">LinqSelectCondition集合</param>
        /// <param name="operatorStr">AND or OR</param>
        /// <returns></returns>
        public static IQueryable<T> Where<T>(
            [NotNull] this IQueryable<T> source,
            [NotNull] IEnumerable<LinqSelectCondition> conditions,
            string operatorStr = "AND"
        )
        {
            return (IQueryable<T>)Where((IQueryable)source, conditions, operatorStr);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="conditions">LinqSelectCondition集合</param>
        /// <param name="operatorStr">AND or OR</param>
        /// <returns></returns>
        public static IQueryable Where(
            [NotNull] this IQueryable source,
            [NotNull] IEnumerable<LinqSelectCondition> conditions,
            string operatorStr = "AND"
        )
        {
            conditions.ToList()
                .ForEach(i =>
                {
                    ParsePropertyInfo(source.ElementType, new List<string>(), $"{i.Field},{i.Operator},{i.Value}".Split('.').ToList(), "where");
                });
            var parameter = Expression.Parameter(source.ElementType, "i");
            Expression expr = null;

            if (_whereFields.Any())
            {
                _whereFields.ForEach(i =>
                {
                    Expression right = ParseExpressionBody(i, parameter, operatorStr);

                    if (expr == null)
                    {
                        expr = right;
                    }
                    else
                    {
                        if (operatorStr.ToUpper() == "AND")
                        {
                            expr = Expression.AndAlso(expr, right);
                        }
                        else
                        {
                            expr = Expression.OrElse(expr, right);
                        }
                    }
                });
            }

            if (expr == null)
            {
                return source;
            }

            var funcType = typeof(Func<,>).MakeGenericType(source.ElementType, typeof(bool));
            LambdaExpression lambda = Expression.Lambda(funcType, expr, parameter);

            source = GetIncludeTableQueryable(source);

            return source.Provider.CreateQuery(
                Expression.Call(
                    typeof(Queryable),
                    "Where",
                    new Type[] { source.ElementType },
                    source.Expression,
                    Expression.Quote(lambda)
                )
            );
        }

        public static IQueryable<T> Select<T>(
            [NotNull] this IQueryable<T> source,
            [NotNull] string selector
        )
        {
            return (IQueryable<T>)Select((IQueryable)source, selector.Split(',').ToList());
        }

        public static IQueryable<T> Select<T>(
            [NotNull] this IQueryable<T> source,
            [NotNull] IEnumerable<string> selector
        )
        {
            return (IQueryable<T>)Select((IQueryable)source, selector);
        }

        public static IQueryable Select(
            [NotNull] this IQueryable source,
            [NotNull] IEnumerable<string> selectors
        )
        {
            foreach (var selector in selectors)
            {
                ParsePropertyInfo(source.ElementType, new List<string>(), selector.Split('.').ToList());
            }

            source = GetIncludeTableQueryable(source);

            return GetSelectFieldQueryable(source);
        }

        /// <summary>
        /// 清空集合
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IQueryable<T> Reset<T>([NotNull] this IQueryable<T> source)
        {
            _includeTables.Clear();
            _tableFields.Clear();
            _whereFields.Clear();
            return source;
        }




        /// <summary>
        /// 生成where参数表达式主体
        /// </summary>
        /// <param name="whereField"></param>
        /// <param name="parameter"></param>
        /// <param name="operatorStr"></param>
        /// <returns></returns>
        private static Expression ParseExpressionBody(
            TableField whereField,
            Expression parameter,
            string operatorStr
        )
        {
            if (whereField.PropertyType == null)
            {
                return ParseCondition(whereField, parameter);
            }
            else if (whereField.Child.Any())
            {
                Expression p = Expression.Property(parameter, whereField.Name);
                Expression cp = !whereField.IsCollection ? p : Expression.Parameter(whereField.PropertyType, $"i_{whereField.Name}");
                Expression body = null;
                whereField.Child.ForEach(i =>
                {
                    Expression right = ParseExpressionBody(i, cp, operatorStr);

                    if (body == null)
                    {
                        body = right;
                    }
                    else
                    {
                        if (operatorStr.ToUpper() == "AND")
                        {
                            body = Expression.AndAlso(body, right);
                        }
                        else
                        {
                            body = Expression.OrElse(body, right);
                        }
                    }
                });

                if (!whereField.IsCollection)
                {
                    return body;
                }

                var funcType = typeof(Func<,>).MakeGenericType(whereField.PropertyType, typeof(bool));
                var lambda = Expression.Lambda(funcType, body, (ParameterExpression)cp);

                var whereExpression = Expression.Call(
                    typeof(Enumerable),
                    "Where",
                    new[] { whereField.PropertyType },
                    p,
                    lambda
                );
                return Expression.Call(
                    typeof(Enumerable),
                    "Any",
                    new[] { whereField.PropertyType },
                    whereExpression
                );
            }
            return Expression.Constant(true, typeof(bool));
        }

        /// <summary>
        /// 对查询条件进行处理
        /// </summary>
        /// <param name="whereField"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        private static Expression ParseCondition(TableField whereField, Expression parameter)
        {
            Expression p = parameter;

            string fieldName = whereField.Name.Substring(0, whereField.Name.IndexOf(','));
            string type = whereField.Name.Substring(fieldName.Length + 1, whereField.Name.IndexOf(',', fieldName.Length + 1) - (fieldName.Length + 1));
            string value = whereField.Name.Substring(fieldName.Length + type.Length + 2);

            //对值进行转换处理
            object convertValue = value;
            if (type != LinqSelectOperator.InWithContains.ToString() && type != LinqSelectOperator.InWithEqual.ToString())
            {
                if (type.ToUpper() == "DATETIME")
                {
                    convertValue = Convert.ToDateTime(value);
                }
                else if (type.ToUpper() == "INT")
                {
                    convertValue = Convert.ToInt32(value);
                }
                else if (type.ToUpper() == "LONG")
                {
                    convertValue = Convert.ToInt64(value);
                }
                else if (type.ToUpper() == "DOUBLE")
                {
                    convertValue = Convert.ToDouble(value);
                }
                else if (type.ToUpper() == "BOOL")
                {
                    convertValue = Convert.ToBoolean(value);
                }
            }

            Expression exprValue = VisitConstant(Expression.Constant(convertValue));

            // 参数化字段
            //if (condition.ParentFields != null && condition.ParentFields.Count > 0)
            //{
            //    foreach (var parent in condition.ParentFields)
            //    {
            //        p = Expression.Property(p, parent);
            //    }
            //}
            p = Expression.Property(p, fieldName);

            if (type.Equals(LinqSelectOperator.Contains.ToString()))
            {
                return Expression.Call(p, typeof(string).GetMethod("Contains", new Type[] { typeof(string) }), exprValue);
            }
            else if (type.Equals(LinqSelectOperator.Equal.ToString()))
            {
                return Expression.Equal(p, Expression.Convert(exprValue, p.Type));
            }
            else if (type.Equals(LinqSelectOperator.Greater.ToString()))
            {
                return Expression.GreaterThan(p, Expression.Convert(exprValue, p.Type));
            }
            else if (type.Equals(LinqSelectOperator.GreaterEqual.ToString()))
            {
                return Expression.GreaterThanOrEqual(p, Expression.Convert(exprValue, p.Type));
            }
            else if (type.Equals(LinqSelectOperator.Less.ToString()))
            {
                return Expression.LessThan(p, Expression.Convert(exprValue, p.Type));
            }
            else if (type.Equals(LinqSelectOperator.LessEqual.ToString()))
            {
                return Expression.LessThanOrEqual(p, Expression.Convert(exprValue, p.Type));
            }
            else if (type.Equals(LinqSelectOperator.NotEqual.ToString()))
            {
                return Expression.NotEqual(p, Expression.Convert(exprValue, p.Type));
            }
            else if (type.Equals(LinqSelectOperator.InWithEqual.ToString()))
            {
                return ParaseIn(p, value, whereField.PropertyType, true);
            }
            else if (type.Equals(LinqSelectOperator.InWithContains.ToString()))
            {
                return ParaseIn(p, value, whereField.PropertyType, false);
            }
            else if (type.Equals(LinqSelectOperator.Between.ToString()))
            {
                return ParaseBetween(p, value, whereField.PropertyType);
            }

            throw new NotImplementedException("不支持此操作");
        }

        /// <summary>
        /// 对查询“Between"的处理
        /// </summary>
        /// <param name="param"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static Expression ParaseBetween(Expression param, string value, Type type)
        {
            //ParameterExpression p = parameter;
            //Expression key = Expression.Property(p, conditions.Field);
            var valueArr = value.Split(',');
            if (valueArr.Length != 2)
            {
                throw new NotImplementedException("ParaseBetween参数错误");
            }
            try
            {
                int.Parse(valueArr[0]);
                int.Parse(valueArr[1]);
            }
            catch
            {
                throw new NotImplementedException("ParaseBetween参数只能为数字");
            }
            //Expression expression = Expression.Constant(true, typeof(bool));
            // 开始位置
            Expression startvalue = Expression.Constant(int.Parse(valueArr[0]));
            Expression start = Expression.GreaterThanOrEqual(param, Expression.Convert(startvalue, type));

            Expression endvalue = Expression.Constant(int.Parse(valueArr[1]));
            Expression end = Expression.GreaterThanOrEqual(param, Expression.Convert(endvalue, type));
            return Expression.AndAlso(start, end);
        }

        /// <summary>
        /// 对查询“in"的处理
        /// </summary>
        /// <param name="key"></param>
        /// <param name="conditions"></param>
        /// <param name="isEqual"></param>
        /// <returns></returns>
        private static Expression ParaseIn(Expression key, string value, Type type, bool isEqual)
        {
            var valueArr = value.Split(',');
            Expression expression = Expression.Constant(false, typeof(bool));
            foreach (var itemVal in valueArr)
            {
                object conValue = itemVal;
                Type keyType = type;
                if (type.Equals(typeof(int)))
                {
                    conValue = Convert.ToInt32(itemVal);
                    keyType = typeof(int);
                }
                else if (type.Equals(typeof(long)))
                {
                    conValue = Convert.ToInt64(itemVal);
                    keyType = typeof(long);
                }
                Expression exprValue = Expression.Constant(conValue);
                Expression right;
                if (isEqual)
                {
                    right = Expression.Equal(key, Expression.Convert(exprValue, keyType));
                }
                else
                {
                    right = Expression.Call(key, typeof(string).GetMethod("Contains", new Type[] { typeof(string) }), exprValue);
                }
                expression = Expression.Or(expression, right);
            }
            return expression;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parentType">父级类型</param>
        /// <param name="parentPropertys">所有父级属性的class type.FullName集合，级别为降序排列</param>
        /// <param name="propertys">剩余关联属性列表，级别为降序排列</param>
        /// <param name="type">类型：where or select</param>
        private static void ParsePropertyInfo(
            Type parentType,
            List<string> parentPropertys,
            List<string> propertys,
            string type = "select"
        )
        {
            // 当前字段
            string rootProperty;
            // 关联模型的字段集合
            var propertyChilds = new List<string>();

            if (propertys.Count > 1)
            {
                rootProperty = propertys.First();
                propertyChilds = propertys.Where((item, index) => !index.Equals(0)).ToList();
            }
            else if (propertys.Count.Equals(1))
            {
                rootProperty = propertys.First();
            }
            else
            {
                return;
            }

            Type rootPropertyType;
            var isCollection = false;

            var rootPropertyInfo = type == "where"
                ? (rootProperty.IndexOf(',') > 0 ? parentType.GetProperty(rootProperty.Substring(0, rootProperty.IndexOf(','))) : parentType.GetProperty(rootProperty))
                : parentType.GetProperty(rootProperty);
            // 模型属性类型判断
            if (rootPropertyInfo.PropertyType.IsGenericType && rootPropertyInfo.PropertyType.HasImplementedRawGeneric(typeof(IEnumerable<>)))
            { // 是集合
                isCollection = true;
                rootPropertyType = rootPropertyInfo.PropertyType.GetGenericArguments().First();
            }
            else
            {
                rootPropertyType = rootPropertyInfo.PropertyType;
            }

            var fieldInfo = new TableField
            {
                Name = rootProperty,
                PropertyInfo = rootPropertyInfo,
                IsCollection = isCollection,
            };

            if (!string.IsNullOrWhiteSpace(ModelNamespace) && rootPropertyType.FullName.IndexOf(ModelNamespace).Equals(0))
            { // 模型
                fieldInfo.Key = rootPropertyType.FullName;
                fieldInfo.PropertyType = rootPropertyType;

                if (parentPropertys.Count.Equals(0))
                {
                    if (!_includeTables.Where(i => i.Name.Equals(rootPropertyType.FullName)).Any())
                    {
                        _includeTables.Add(new IncludeTable
                        {
                            Name = rootPropertyType.FullName,
                            PropertyInfo = rootPropertyInfo,
                            PropertyType = rootPropertyType,
                        });
                    }

                    if (type == "where")
                    {
                        if (!_whereFields.Where(i => i.Key.Equals(fieldInfo.Key)).Any())
                        {
                            _whereFields.Add(fieldInfo);
                        }
                    }
                    else
                    {
                        if (!_tableFields.Where(i => i.Key.Equals(fieldInfo.Key)).Any())
                        {
                            _tableFields.Add(fieldInfo);
                        }
                    }
                }
                else
                {
                    IncludeTable parentTable = null;
                    TableField parentTableField = null;
                    foreach (var parentProperty in parentPropertys)
                    { // 通过父子级关系添加
                        if (parentPropertys.IndexOf(parentProperty).Equals(0))
                        {
                            parentTable = _includeTables.Where(i => i.Name.Equals(parentProperty)).First();

                            if (type == "where")
                            {
                                parentTableField = _whereFields.Where(i => i.Key.Equals(parentProperty)).First();
                            }
                            else
                            {
                                parentTableField = _tableFields.Where(i => i.Key.Equals(parentProperty)).First();
                            }
                        }
                        else
                        {
                            parentTable = parentTable.Child.Where(i => i.Name.Equals(parentProperty)).First();
                            parentTableField = parentTableField.Child.Where(i => i.Key.Equals(parentProperty)).First();
                        }
                    }

                    if (parentTable != null && !parentTable.Child.Where(i => i.Name.Equals(rootPropertyType.FullName)).Any())
                    {
                        parentTable.Child.Add(new IncludeTable
                        {
                            Name = rootPropertyType.FullName,
                            PropertyInfo = rootPropertyInfo,
                            PropertyType = rootPropertyType,
                        });
                    }

                    if (parentTableField != null && !parentTableField.Child.Where(i => i.Key.Equals(fieldInfo.Key)).Any())
                    {
                        parentTableField.Child.Add(fieldInfo);
                    }
                }

                if (propertyChilds.Any())
                {
                    parentPropertys.Add(rootPropertyType.FullName);
                    ParsePropertyInfo(rootPropertyType, parentPropertys, propertyChilds, type);
                }
            }
            else
            { // 非模型
                fieldInfo.Key = $"{rootPropertyType.FullName}." + (rootProperty.IndexOf(',') > 0 ? rootProperty.Substring(0, rootProperty.IndexOf(',')) : rootProperty);

                if (parentPropertys.Count.Equals(0))
                {
                    if (type == "where")
                    {
                        _whereFields.Add(fieldInfo);
                    }
                    else
                    {
                        _tableFields.Add(fieldInfo);
                    }
                }
                else
                {
                    TableField parentTableField = null;
                    foreach (var parentProperty in parentPropertys)
                    {
                        if (parentPropertys.IndexOf(parentProperty).Equals(0))
                        {
                            if (type == "where")
                            {
                                parentTableField = _whereFields.Where(i => i.Key.Equals(parentProperty)).First();
                            }
                            else
                            {
                                parentTableField = _tableFields.Where(i => i.Key.Equals(parentProperty)).First();
                            }
                        }
                        else
                        {
                            parentTableField = parentTableField.Child.Where(i => i.Key.Equals(parentProperty)).First();
                        }
                    }

                    if (parentTableField != null)
                    {
                        parentTableField.Child.Add(fieldInfo);
                    }
                }
            }
        }


        //---- 自动识别并加载关联表 ----

        /// <summary>
        /// include关联
        /// </summary>
        /// <param name="queryable"></param>
        /// <returns></returns>
        private static IQueryable GetIncludeTableQueryable(IQueryable queryable)
        {
            foreach (var includeTable in _includeTables)
            {
                var param = Expression.Parameter(queryable.ElementType, "rec");
                var funcType = typeof(Func<,>).MakeGenericType(queryable.ElementType, includeTable.PropertyInfo.PropertyType);
                LambdaExpression lambda = Expression.Lambda(funcType, Expression.Property(param, includeTable.PropertyInfo), param);
                queryable = queryable.Provider.CreateQuery(
                    Expression.Call(
                        typeof(EntityFrameworkQueryableExtensions),
                        "Include",
                        new Type[] { queryable.ElementType, lambda.Body.Type },
                        queryable.Expression,
                        Expression.Quote(lambda)
                    )
                );

                if (includeTable.Child.Any())
                {
                    queryable = GetThenIncludeTableQueryable(queryable, includeTable);
                }
            }

            return queryable;
        }

        /// <summary>
        /// thenInclude关联
        /// </summary>
        /// <param name="queryable"></param>
        /// <param name="parentIncludeTable">父级表 IncludeTable 类</param>
        /// <returns></returns>
        private static IQueryable GetThenIncludeTableQueryable(IQueryable queryable, IncludeTable parentIncludeTable)
        {
            foreach (var includeTable in parentIncludeTable.Child)
            {
                var param = Expression.Parameter(parentIncludeTable.PropertyType, "rec");
                var funcType = typeof(Func<,>).MakeGenericType(parentIncludeTable.PropertyType, includeTable.PropertyInfo.PropertyType);

                LambdaExpression lambda = Expression.Lambda(funcType, Expression.Property(param, includeTable.PropertyInfo), param);

                queryable = queryable.Provider.CreateQuery(
                    Expression.Call(
                        typeof(EntityFrameworkQueryableExtensions),
                        "ThenInclude",
                        new Type[] { queryable.ElementType, parentIncludeTable.PropertyType, lambda.Body.Type },
                        queryable.Expression,
                        Expression.Quote(lambda)
                    )
                );

                if (includeTable.Child.Any())
                {
                    queryable = GetThenIncludeTableQueryable(queryable, includeTable);
                }
            }

            return queryable;
        }


        //---- 自动组装select查询LambdaExpression表达式 ----

        private static IQueryable GetSelectFieldQueryable(IQueryable queryable)
        {
            // (rec)
            var param = Expression.Parameter(queryable.ElementType, "rec");
            //new ParadigmSearchListData
            var v0 = Expression.New(queryable.ElementType);

            var memberBindings = new List<MemberBinding>();

            foreach (var field in _tableFields)
            {
                var k = field.PropertyInfo;
                if (k != null)
                {
                    if (field.PropertyType == null || !field.Child.Any())
                    {
                        var v = Expression.Property(param, k);
                        memberBindings.Add(Expression.Bind(k, v));
                    }
                    else
                    {
                        var v = GetSelectFieldQueryable(field, param);
                        memberBindings.Add(Expression.Bind(k, v));
                    }
                }
            }

            var funcType = typeof(Func<,>).MakeGenericType(queryable.ElementType, queryable.ElementType);
            var lambda = Expression.Lambda(funcType, Expression.MemberInit(v0, memberBindings), param);

            return queryable.Provider.CreateQuery(
                Expression.Call(
                    typeof(Queryable),
                    "Select",
                    new Type[] { queryable.ElementType, lambda.Body.Type },
                    queryable.Expression,
                    Expression.Quote(lambda)
                )
            );
        }

        private static Expression GetSelectFieldQueryable(TableField parentField, Expression param, int level = 0)
        {
            var selectParamExpression = Expression.Property(param, parentField.PropertyInfo);

            var newExpression = Expression.New(parentField.PropertyType);
            var memberBindings = new List<MemberBinding>();
            Expression parameterExpressionP;

            if (level.Equals(0) || parentField.IsCollection)
            {
                parameterExpressionP = Expression.Parameter(parentField.PropertyType, $"p{level}");
            }
            else
            {
                parameterExpressionP = Expression.Property(param, parentField.PropertyInfo);
            }

            foreach (var field in parentField.Child)
            {
                var k = field.PropertyInfo;
                if (k != null)
                {
                    if (field.PropertyType == null || !field.Child.Any())
                    {
                        var v = Expression.Property(parameterExpressionP, k);
                        memberBindings.Add(Expression.Bind(k, v));
                    }
                    else
                    {
                        var v = GetSelectFieldQueryable(field, parameterExpressionP, level + 1);
                        memberBindings.Add(Expression.Bind(k, v));
                    }
                }
            }

            if (parentField.IsCollection)
            {
                if (memberBindings.Any())
                {
                    var funcType = typeof(Func<,>).MakeGenericType(parentField.PropertyType, parentField.PropertyType);
                    var lambda = Expression.Lambda(funcType, Expression.MemberInit(newExpression, memberBindings), (ParameterExpression)parameterExpressionP);

                    var selectExpression = Expression.Call(
                        typeof(Enumerable),
                        "Select",
                        new[] { parentField.PropertyType, parentField.PropertyType },
                        selectParamExpression,
                        lambda
                    );
                    return Expression.Call(
                        typeof(Enumerable),
                        "ToList",
                        new[] { parentField.PropertyType },
                        selectExpression
                    );
                }
                else
                {
                    return Expression.Call(
                        typeof(Enumerable),
                        "ToList",
                        new[] { parentField.PropertyType },
                        selectParamExpression
                    );
                }
            }
            else
            {
                return Expression.MemberInit(newExpression, memberBindings);
            }
        }

        /// <summary>
        /// Visit the constant expression
        /// </summary>
        /// <param name="node">The original expression node</param>
        /// <returns>The new PropertyExpression</returns>
        private static Expression VisitConstant(ConstantExpression node)
        {
            if (ParameterizableTypes.TryGetValue(node.Type, out Type tupleType))
            {
                //Replace the ConstantExpression to PropertyExpression of Turple<T>.Item1
                //So EF5 can parameterize it when compile the expression tree
                object wrappedObject = Activator.CreateInstance(tupleType, new[] { node.Value });
                Expression visitedExpression = Expression.Property(Expression.Constant(wrappedObject), "Item1");
                return visitedExpression;
            }
            return node;
        }
    }
}
