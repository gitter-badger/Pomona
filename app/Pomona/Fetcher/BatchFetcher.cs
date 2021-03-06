﻿#region License

// Pomona is open source software released under the terms of the LICENSE specified in the
// project's repository, or alternatively at http://pomona.io/

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Pomona.Common.Internals;

namespace Pomona.Fetcher
{
    using FetchEntitiesByIdInBatches = Func<Type, Type, BatchFetcher, object[], PropertyInfo, IEnumerable<object>>;

    public class BatchFetcher
    {
        private static readonly MethodInfo containsMethod;
        private static readonly MethodInfo expandCollectionBatchedMethod;
        private static readonly MethodInfo expandManyToOneMethod;
        private static readonly Action<Type, BatchFetcher, object, string> expandMethod;
        private readonly IBatchFetchDriver driver;
        private readonly HashSet<string> expandedPaths;
        private readonly FetchEntitiesByIdInBatches fetchEntitiesByIdInBatches;


        static BatchFetcher()
        {
            containsMethod = ReflectionHelper.GetMethodDefinition<IEnumerable<object>>(x => x.Contains(null));
            expandCollectionBatchedMethod = ReflectionHelper.GetMethodDefinition<BatchFetcher>(
                x => x.ExpandCollectionBatched<object, object, object>(null, null, null));
            expandManyToOneMethod = ReflectionHelper.GetMethodDefinition<BatchFetcher>(
                x => x.ExpandManyToOne<object, object>(null, null, null));
            expandMethod = GenericInvoker.Instance<BatchFetcher>().CreateAction1<object, string>(
                x => x.Expand<object>(null, null));
        }


        public BatchFetcher(IBatchFetchDriver driver, string expandedPaths)
            : this(driver, expandedPaths, 100)
        {
        }


        public BatchFetcher(IBatchFetchDriver driver, string expandedPaths, int batchFetchCount)
        {
            this.fetchEntitiesByIdInBatches = GenericInvoker
                .Instance<BatchFetcher>()
                .CreateFunc2<object[], PropertyInfo, IEnumerable<object>>(
                    x => x.FetchEntitiesByIdInBatches<object, object>(null, null));

            this.expandedPaths = ExpandPathsUtils.GetExpandedPaths(expandedPaths);
            this.driver = driver;
            BatchFetchCount = batchFetchCount;
        }


        public int BatchFetchCount { get; }


        public void Expand(object entitiesUncast, Type entityType)
        {
            var entities = entitiesUncast as IEnumerable;
            if (entities == null)
            {
                if (entitiesUncast == null)
                    throw new InvalidOperationException("Unexpected entity type sent to Expand method.");
                entities = new[] { entitiesUncast };
            }

            Expand(entities);
        }


        public void Expand<TEntity>(IEnumerable<TEntity> entities, string path = "")
            where TEntity : class
        {
            foreach (var prop in this.driver.GetProperties(typeof(TEntity)))
            {
                var subPath = string.IsNullOrEmpty(path) ? prop.Name : path + "." + prop.Name;
                if (this.expandedPaths.Contains(subPath.ToLower()) || this.driver.PathIsExpanded(subPath, prop))
                {
                    if (IsManyToOne(prop))
                    {
                        expandManyToOneMethod.MakeGenericMethod(typeof(TEntity), prop.PropertyType)
                                             .Invoke(this, new object[] { entities, subPath, prop });
                    }
                    else
                    {
                        Type elementType;
                        if (IsCollection(prop, out elementType))
                        {
                            expandCollectionBatchedMethod.MakeGenericMethod(typeof(TEntity), elementType,
                                                                            this.driver.GetIdProperty(typeof(TEntity))
                                                                                .PropertyType)
                                                         .Invoke(this, new object[] { entities, subPath, prop });
                        }
                    }
                }
            }
        }


        protected virtual void ExpandCollection<TParentEntity, TCollectionElement, TParentId>(
            IEnumerable<TParentEntity> entities,
            string path,
            PropertyInfo prop,
            PropertyInfo parentIdProp,
            Func<TParentEntity, TParentId> getParentIdExpr)
            where TCollectionElement : class
        {
            PropertyInfo childIdProp;
            var getChildIdExpr = CreateIdGetExpression<TCollectionElement>(out childIdProp).Compile();

            var getChildLambdaParam = Expression.Parameter(typeof(TParentEntity), "z");
            var childProperty = Expression.Convert(Expression.Property(getChildLambdaParam, prop),
                                                   typeof(IEnumerable<TCollectionElement>));
            var getChildLambda = Expression
                .Lambda<Func<TParentEntity, IEnumerable<TCollectionElement>>>(childProperty, getChildLambdaParam)
                .Compile();

            var parentEntities = entities
                .Select(x => new { Parent = x, Collection = getChildLambda(x), ParentId = getParentIdExpr(x) })
                .ToList();

            var parentIdsToFetch = parentEntities
                .Where(x => x.Collection != null && !this.driver.IsLoaded(x.Collection))
                .Select(x => x.ParentId)
                .Distinct()
                .ToArray();

            if (parentIdsToFetch.Length == 0)
                return;

            var containsExprParam = Expression.Parameter(typeof(TParentEntity), "tp");
            var containsExpr = Expression.Lambda<Func<TParentEntity, bool>>(
                Expression.Call(containsMethod.MakeGenericMethod(typeof(TParentId)),
                                Expression.Constant(parentIdsToFetch),
                                Expression.Property(containsExprParam, parentIdProp)),
                containsExprParam);
            //var lineOrderIdMap =
            //    db.Orders
            //    .Where(x => ordersIds.Contains(x.OrderId))
            //    .SelectMany(x => x.OrderLines.Select(y => new ParentChildRelation(x.OrderId, y.Id)))
            //    .ToList();

            var selectManyExprParam = Expression.Parameter(typeof(TParentEntity), "x");
            var selectManyExpr = Expression.Lambda<Func<TParentEntity, IEnumerable<TCollectionElement>>>(
                Expression.Property(selectManyExprParam, prop),
                selectManyExprParam);

            var selectRelationLeftParam = Expression.Parameter(typeof(TParentEntity), "a");
            var selectRelationRightParam = Expression.Parameter(typeof(TCollectionElement), "b");
            var selectRelation = Expression.Lambda<Func<TParentEntity, TCollectionElement, ParentChildRelation>>(
                Expression.New(typeof(ParentChildRelation).GetConstructors().First(),
                               Expression.Convert(Expression.Property(selectRelationLeftParam, parentIdProp),
                                                  typeof(object)),
                               Expression.Convert(Expression.Property(selectRelationRightParam, childIdProp),
                                                  typeof(object))),
                selectRelationLeftParam,
                selectRelationRightParam);

            var relations = this.driver.Query<TParentEntity>()
                                .Where(containsExpr)
                                .SelectMany(selectManyExpr, selectRelation)
                                .ToList();

            var childIdsToFetch = relations.Select(x => x.ChildId).Distinct().ToArray();

            var fetched =
                FetchEntitiesByIdInBatches<TCollectionElement>(childIdsToFetch, childIdProp)
                    .ToDictionary(getChildIdExpr, x => x);

            var bindings =
                parentEntities
                    .Join(parentIdsToFetch, x => x.ParentId, x => x, (a, b) => a)
                    // Only bind collections in parentIdsToFetch
                    .GroupJoin(relations,
                               x => x.ParentId,
                               x => x.ParentId,
                               (a, b) =>
                                   new KeyValuePair<TParentEntity, IEnumerable<TCollectionElement>>
                                   (a.Parent, b.Select(y => fetched[y.ChildId])));
            this.driver.PopulateCollections(bindings, prop, typeof(TCollectionElement));

            Expand(parentEntities.SelectMany(x => getChildLambda(x.Parent)), path);
        }


        protected virtual void ExpandCollectionBatched<TParentEntity, TCollectionElement, TParentId>(
            IEnumerable<TParentEntity> entities,
            string path,
            PropertyInfo prop)
            where TCollectionElement : class
        {
            PropertyInfo parentIdProp;
            var getParentIdExpr = CreateIdGetExpression<TParentEntity, TParentId>(out parentIdProp).Compile();
            Partition(entities.Distinct().OrderBy(getParentIdExpr), BatchFetchCount)
                .ToList()
                .ForEach(x => ExpandCollection<TParentEntity, TCollectionElement, TParentId>(x,
                                                                                             path,
                                                                                             prop,
                                                                                             parentIdProp,
                                                                                             getParentIdExpr));
        }


        protected virtual IEnumerable<TEntity> FetchEntitiesById<TEntity, TId>(TId[] ids, PropertyInfo idProp)
        {
            if (idProp == null)
                throw new ArgumentNullException(nameof(idProp));
            var fetchPredicateParam = Expression.Parameter(typeof(TEntity), "x");
            var fetchPredicate = Expression.Lambda<Func<TEntity, bool>>(
                Expression.Call(
                    containsMethod.MakeGenericMethod(idProp.PropertyType),
                    Expression.Constant(ids),
                    Expression.Property(fetchPredicateParam, idProp)),
                fetchPredicateParam);
            var results = this.driver.Query<TEntity>()
                              .Where(fetchPredicate)
                              .ToList();
            return results;
        }


        private Expression<Func<TEntity, object>> CreateIdGetExpression<TEntity>(out PropertyInfo idProp)
        {
            return CreateIdGetExpression<TEntity, object>(out idProp);
        }


        private Expression<Func<TEntity, TId>> CreateIdGetExpression<TEntity, TId>(out PropertyInfo idProp)
        {
            idProp = this.driver.GetIdProperty(typeof(TEntity));
            var refParam = Expression.Parameter(typeof(TEntity), "ref");
            Expression propExpr = Expression.Property(refParam, idProp);
            if (propExpr.Type != typeof(TId))
                propExpr = Expression.Convert(propExpr, typeof(TId));
            var idGetter =
                Expression.Lambda<Func<TEntity, TId>>(
                    propExpr,
                    refParam);
            return idGetter;
        }


        private void Expand(IEnumerable entitiesUncast, string path = "")
        {
            var entitiesGroupedByType =
                entitiesUncast
                    .Cast<object>()
                    .Where(x => x != null)
                    .GroupBy(x => x.GetType())
                    .ToList();

            entitiesGroupedByType.ForEach(x => expandMethod(x.Key, this, x.Cast(x.Key), path));
        }


        private void ExpandManyToOne<TParentEntity, TReferencedEntity>(IEnumerable<TParentEntity> entities,
                                                                       string path,
                                                                       PropertyInfo prop)
            where TReferencedEntity : class
        {
            PropertyInfo idProp;
            var idGetter = CreateIdGetExpression<TReferencedEntity>(out idProp).Compile();

            var objectsToWalk = entities
                .Select(x => new { Parent = x, Reference = (TReferencedEntity)prop.GetValue(x, null) })
                .Where(x => x.Reference != null)
                .Select(x => new { x.Parent, x.Reference, RefId = idGetter(x.Reference) })
                .ToList();

            var objectsToExpand = objectsToWalk
                .Where(x => !this.driver.IsLoaded(x.Reference))
                .ToList();

            var ids = objectsToExpand.Select(x => idGetter(x.Reference)).Distinct().ToArray();

            foreach (
                var item in
                    FetchEntitiesByIdInBatches<TReferencedEntity>(ids, idProp)
                        .Join(objectsToExpand, idGetter, x => x.RefId, (a, b) => new { Child = a, b.Parent }))
                prop.SetValue(item.Parent, item.Child, null);

            Expand(objectsToWalk.Select(x => x.Reference), path);
        }


        private IEnumerable FetchEntitiesByIdInBatches<TEntity, TId>(object[] ids, PropertyInfo idProp)
        {
            return
                Partition(ids.Cast<TId>().OrderBy(x => x), BatchFetchCount).SelectMany(
                    x => FetchEntitiesById<TEntity, TId>(x, idProp));
        }


        private IEnumerable<TEntity> FetchEntitiesByIdInBatches<TEntity>(object[] ids, PropertyInfo idProp)
        {
            var entities = this.fetchEntitiesByIdInBatches(typeof(TEntity), idProp.PropertyType, this, ids, idProp);
            return ((IEnumerable<TEntity>)entities).ToList();
        }


        private bool IsCollection(PropertyInfo prop, out Type elementType)
        {
            elementType = null;
            if (prop.PropertyType == typeof(string))
                return false;

            elementType = prop.PropertyType
                              .GetInterfaces()
                              .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                              .Select(x => x.GetGenericArguments()[0])
                              .FirstOrDefault();

            return elementType != null;
        }


        private bool IsManyToOne(PropertyInfo prop)
        {
            return this.driver.IsManyToOne(prop);
        }


        private static IEnumerable<T[]> Partition<T>(IEnumerable<T> source, int partLength)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (partLength < 1)
                throw new ArgumentOutOfRangeException(nameof(partLength), "Can't divide sequence in parts less than length 1");
            T[] part = null;
            var offset = 0;
            foreach (var item in source)
            {
                if (offset >= partLength)
                {
                    yield return part;
                    offset = 0;
                    part = null;
                }

                if (part == null)
                    part = new T[partLength];

                part[offset++] = item;
            }

            if (part != null)
            {
                if (offset < partLength)
                    Array.Resize(ref part, offset);
                yield return part;
            }
        }
    }
}