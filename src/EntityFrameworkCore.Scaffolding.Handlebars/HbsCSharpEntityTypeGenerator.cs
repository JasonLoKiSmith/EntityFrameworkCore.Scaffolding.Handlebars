﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Modifications copyright(C) 2017 Tony Sneed.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using EntityFrameworkCore.Scaffolding.Handlebars.Helpers;
using EntityFrameworkCore.Scaffolding.Handlebars.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using HandlebarsLib = HandlebarsDotNet.Handlebars;

namespace EntityFrameworkCore.Scaffolding.Handlebars
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and may change or be removed in future releases.
    /// </summary>
    public class HbsCSharpEntityTypeGenerator : ICSharpEntityTypeGenerator
    {
        private ICSharpUtilities CSharpUtilities { get; }
        private readonly IEntityTypeTemplateService _entityTypeTemplateService;

        private IndentedStringBuilder _sb;
        private bool _useDataAnnotations;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public HbsCSharpEntityTypeGenerator(
            ICSharpUtilities cSharpUtilities,
            IEntityTypeTemplateService entityTypeTemplateService)
        {
            CSharpUtilities = cSharpUtilities ?? throw new ArgumentNullException(nameof(cSharpUtilities));
            _entityTypeTemplateService = entityTypeTemplateService ?? throw new ArgumentNullException(nameof(entityTypeTemplateService));
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual string WriteCode(IEntityType entityType, string @namespace, bool useDataAnnotations)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            if (@namespace == null) throw new ArgumentNullException(nameof(@namespace));

            // Register spaces helper
            HandlebarsLib.RegisterHelper(Constants.SpacesHelper, HandlebarsHelpers.GetSpacesHelper());

            // Register partial templates
            foreach (var partialTemplate in _entityTypeTemplateService.GetEntityTypePartialTemplates())
            {
                HandlebarsLib.RegisterTemplate(partialTemplate.Key, partialTemplate.Value);
            }

            _sb = new IndentedStringBuilder();
            _useDataAnnotations = useDataAnnotations;

            _sb.AppendLine("using System;");
            _sb.AppendLine("using System.Collections.Generic;");

            if (_useDataAnnotations)
            {
                _sb.AppendLine("using System.ComponentModel.DataAnnotations;");
                _sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
            }

            foreach (var ns in entityType.GetProperties()
                .SelectMany(p => p.ClrType.GetNamespaces())
                .Where(ns => ns != "System" && ns != "System.Collections.Generic")
                .Distinct())
            {
                _sb.AppendLine($"using {ns};");
            }

            _sb.AppendLine();
            _sb.AppendLine($"namespace {@namespace}");
            _sb.AppendLine("{");

            using (_sb.Indent())
            {
                GenerateClass(entityType);
            }

            _sb.AppendLine("}");

            return _sb.ToString();
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected virtual void GenerateClass(
            IEntityType entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));

            if (_useDataAnnotations)
            {
                GenerateEntityTypeDataAnnotations(entityType);
            }

            _sb.AppendLine($"public partial class {entityType.Name}");

            _sb.AppendLine("{");

            using (_sb.Indent())
            {
                GenerateConstructor(entityType);
                GenerateProperties(entityType);
                GenerateNavigationProperties(entityType);
            }

            _sb.AppendLine("}");
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected virtual void GenerateEntityTypeDataAnnotations(
            IEntityType entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));

            GenerateTableAttribute(entityType);
        }

        private void GenerateTableAttribute(IEntityType entityType)
        {
            var tableName = entityType.Relational().TableName;
            var schema = entityType.Relational().Schema;
            var defaultSchema = entityType.Model.Relational().DefaultSchema;

            var schemaParameterNeeded = schema != null && schema != defaultSchema;
            var tableAttributeNeeded = schemaParameterNeeded || tableName != null && tableName != entityType.Scaffolding().DbSetName;

            if (tableAttributeNeeded)
            {
                var tableAttribute = new AttributeWriter(nameof(TableAttribute));

                tableAttribute.AddParameter(CSharpUtilities.DelimitString(tableName));

                if (schemaParameterNeeded)
                {
                    tableAttribute.AddParameter($"{nameof(TableAttribute.Schema)} = {CSharpUtilities.DelimitString(schema)}");
                }

                _sb.AppendLine(tableAttribute.ToString());
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected virtual void GenerateConstructor(
            IEntityType entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));

            var collectionNavigations = entityType.GetNavigations().Where(n => n.IsCollection()).ToList();

            if (collectionNavigations.Count > 0)
            {
                _sb.AppendLine($"public {entityType.Name}()");
                _sb.AppendLine("{");

                using (_sb.Indent())
                {
                    foreach (var navigation in collectionNavigations)
                    {
                        _sb.AppendLine($"{navigation.Name} = new HashSet<{navigation.GetTargetType().Name}>();");
                    }
                }

                _sb.AppendLine("}");
                _sb.AppendLine();
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected virtual void GenerateProperties(
            IEntityType entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));

            foreach (var property in entityType.GetProperties().OrderBy(p => p.Scaffolding().ColumnOrdinal))
            {
                if (_useDataAnnotations)
                {
                    GeneratePropertyDataAnnotations(property);
                }

                _sb.AppendLine($"public {CSharpUtilities.GetTypeName(property.ClrType)} {property.Name} {{ get; set; }}");
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected virtual void GeneratePropertyDataAnnotations(
            IProperty property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));

            GenerateKeyAttribute(property);
            GenerateRequiredAttribute(property);
            GenerateColumnAttribute(property);
            GenerateMaxLengthAttribute(property);
        }

        private void GenerateKeyAttribute(IProperty property)
        {
            var key = property.AsProperty().PrimaryKey;

            if (key?.Properties.Count == 1)
            {
                if (key is Key concreteKey
                    && key.Properties.SequenceEqual(new KeyDiscoveryConvention().DiscoverKeyProperties(concreteKey.DeclaringEntityType, concreteKey.DeclaringEntityType.GetProperties().ToList())))
                {
                    return;
                }

                if (key.Relational().Name != ConstraintNamer.GetDefaultName(key))
                {
                    return;
                }

                _sb.AppendLine(new AttributeWriter(nameof(KeyAttribute)));
            }
        }

        private void GenerateColumnAttribute(IProperty property)
        {
            var columnName = property.Relational().ColumnName;
            var columnType = property.GetConfiguredColumnType();

            var delimitedColumnName = columnName != null && columnName != property.Name ? CSharpUtilities.DelimitString(columnName) : null;
            var delimitedColumnType = columnType != null ? CSharpUtilities.DelimitString(columnType) : null;

            if ((delimitedColumnName ?? delimitedColumnType) != null)
            {
                var columnAttribute = new AttributeWriter(nameof(ColumnAttribute));

                if (delimitedColumnName != null)
                {
                    columnAttribute.AddParameter(delimitedColumnName);
                }

                if (delimitedColumnType != null)
                {
                    columnAttribute.AddParameter($"{nameof(ColumnAttribute.TypeName)} = {delimitedColumnType}");
                }

                _sb.AppendLine(columnAttribute);
            }
        }

        private void GenerateMaxLengthAttribute(IProperty property)
        {
            var maxLength = property.GetMaxLength();

            if (maxLength.HasValue)
            {
                var lengthAttribute = new AttributeWriter(
                    property.ClrType == typeof(string)
                        ? nameof(StringLengthAttribute)
                        : nameof(MaxLengthAttribute));

                lengthAttribute.AddParameter(CSharpUtilities.GenerateLiteral(maxLength.Value));

                _sb.AppendLine(lengthAttribute.ToString());
            }
        }

        private void GenerateRequiredAttribute(IProperty property)
        {
            if (!property.IsNullable
                && property.ClrType.IsNullableType()
                && !property.IsPrimaryKey())
            {
                _sb.AppendLine(new AttributeWriter(nameof(RequiredAttribute)).ToString());
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected virtual void GenerateNavigationProperties(
            IEntityType entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));

            var sortedNavigations = entityType.GetNavigations()
                .OrderBy(n => n.IsDependentToPrincipal() ? 0 : 1)
                .ThenBy(n => n.IsCollection() ? 1 : 0);

            if (sortedNavigations.Any())
            {
                _sb.AppendLine();

                foreach (var navigation in sortedNavigations)
                {
                    if (_useDataAnnotations)
                    {
                        GenerateNavigationDataAnnotations(navigation);
                    }

                    var referencedTypeName = navigation.GetTargetType().Name;
                    var navigationType = navigation.IsCollection() ? $"ICollection<{referencedTypeName}>" : referencedTypeName;
                    _sb.AppendLine($"public {navigationType} {navigation.Name} {{ get; set; }}");
                }
            }
        }

        private void GenerateNavigationDataAnnotations(INavigation navigation)
        {
            GenerateForeignKeyAttribute(navigation);
            GenerateInversePropertyAttribute(navigation);
        }

        private void GenerateForeignKeyAttribute(INavigation navigation)
        {
            if (navigation.IsDependentToPrincipal())
            {
                if (navigation.ForeignKey.PrincipalKey.IsPrimaryKey())
                {
                    var foreignKeyAttribute = new AttributeWriter(nameof(ForeignKeyAttribute));

                    foreignKeyAttribute.AddParameter(
                        CSharpUtilities.DelimitString(
                            string.Join(",", navigation.ForeignKey.Properties.Select(p => p.Name))));

                    _sb.AppendLine(foreignKeyAttribute.ToString());
                }
            }
        }

        private void GenerateInversePropertyAttribute(INavigation navigation)
        {
            if (navigation.ForeignKey.PrincipalKey.IsPrimaryKey())
            {
                var inverseNavigation = navigation.FindInverse();

                if (inverseNavigation != null)
                {
                    var inversePropertyAttribute = new AttributeWriter(nameof(InversePropertyAttribute));

                    inversePropertyAttribute.AddParameter(CSharpUtilities.DelimitString(inverseNavigation.Name));

                    _sb.AppendLine(inversePropertyAttribute.ToString());
                }
            }
        }

        private class AttributeWriter
        {
            private readonly string _attibuteName;
            private readonly List<string> _parameters = new List<string>();

            public AttributeWriter(string attributeName)
            {
                _attibuteName = attributeName ?? throw new ArgumentNullException(nameof(attributeName));
            }

            public void AddParameter(string parameter)
            {
                if (parameter == null) throw new ArgumentNullException(nameof(parameter));

                _parameters.Add(parameter);
            }

            public override string ToString()
                => "[" + (_parameters.Count == 0
                       ? StripAttribute(_attibuteName)
                       : StripAttribute(_attibuteName) + "(" + string.Join(", ", _parameters) + ")") + "]";

            private static string StripAttribute(string attributeName)
            {
                if (attributeName == null) throw new ArgumentNullException(nameof(attributeName));
                return attributeName.EndsWith("Attribute", StringComparison.Ordinal)
                    ? attributeName.Substring(0, attributeName.Length - 9)
                    : attributeName;
            }
        }
    }
}