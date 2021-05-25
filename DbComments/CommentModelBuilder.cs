using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EFCore.DbComments
{
    /// <summary> Builder for <see cref="CommentModel"/> </summary>
    public class CommentModelBuilder
    {
        private const string DefaultDiscriminatorComment = "Discriminator";

        private string? _discriminatorComment;

        private bool? _enableXmlComments;

        private bool? _enableDescriptionComments;

        /// <summary> Add comment for discriminator </summary>
        public CommentModelBuilder WithDiscriminatorComment(string value)
        {
            _discriminatorComment = value;

            return this;
        }

        /// <summary> Enable extracting comments from xml </summary>
        public CommentModelBuilder WithXmlComments()
        {
            _enableXmlComments = true;

            return this;
        }

        /// <summary> Enable extracting comments from <see cref="DescriptionAttribute"/> </summary>
        public CommentModelBuilder WithDescriptionComments()
        {
            _enableDescriptionComments = true;
            return this;
        }

        /// <summary> Creates <see cref="CommentModel"/> </summary>
        public CommentModel Build(ModelBuilder modelBuilder)
        {
            if (modelBuilder is null)
            {
                throw new ArgumentNullException(nameof(modelBuilder));
            }

            var commentModel = new CommentModel(modelBuilder, _discriminatorComment ?? DefaultDiscriminatorComment);

            if (_enableXmlComments == true)
            {
                FillCommentsFromXml(commentModel);
            }

            if (_enableDescriptionComments == true)
            {
                FillCommentsFromDescription(commentModel);
            }

            return commentModel;
        }

        private void FillCommentsFromDescription(CommentModel commentModel)
        {
            foreach (var entity in commentModel.EntityComments)
            {
                if (entity.EntityType.ClrType.GetCustomAttribute(typeof(DescriptionAttribute)) is DescriptionAttribute { Description: not null } entityDescriptionAttr)
                {
                    entity.InternalComment = new CommentModel.CommentFromType(entityDescriptionAttr.Description, entity.EntityType.ClrType);
                }

                foreach (var property in entity.EntityProperties)
                {
                    if (property.Property.IsShadowProperty())
                    {
                        continue;
                    }

                    if (property.Property.PropertyInfo.GetCustomAttribute(typeof(DescriptionAttribute)) is DescriptionAttribute { Description: not null } propDescriptionAttr)
                    {
                        property.Comment = propDescriptionAttr.Description;
                    }
                }
            }
        }

        private void FillCommentsFromXml(CommentModel commentModel)
        {
            var assemblies = EnumerateAssemblies(commentModel.EntityComments.Select(x => x.EntityType.ClrType));
            var xmlDocFilePaths = assemblies.Select(x => Path.Combine(AppContext.BaseDirectory, $"{x.GetName().Name}.xml"));
            foreach (var xmlDocFilePath in xmlDocFilePaths)
            {
                if (!File.Exists(xmlDocFilePath))
                {
                    continue;
                }

                var xdoc = XDocument.Load(xmlDocFilePath);
                var xElements = xdoc.Element("doc")?.Element("members")?.Elements("member");

                if (xElements is null)
                {
                    continue;
                }

                var commentDict = xElements
                    .Select(x => (Name: x.Attribute("name")?.Value, Value: x.Element("summary")?.Value.Trim()))
                    .GroupBy(x => x.Name).Where(x => x.Key is not null)
                    .ToImmutableDictionary(x => x.Key!, x => x.First().Value);

                foreach (var entity in commentModel.EntityComments)
                {
                    var entityComment = GetEntityComment(commentDict, entity.EntityType);

                    if (entityComment is not null && (entity.InternalComment is null || entityComment.Value.HolderType.IsSubclassOf(entity.InternalComment.Value.HolderType)))
                    {
                        entity.InternalComment = entityComment;
                    }

                    foreach (var property in entity.EntityProperties)
                    {
                        var propertyComment = GetPropertyComment(commentDict, entity.EntityType.ClrType, property.Property);

                        if (propertyComment is { })
                        {
                            property.Comment = propertyComment;
                        }
                    }

                    foreach (var navigation in entity.EntityNavigations)
                    {
                        var comment = GetPropertyComment(commentDict, entity.EntityType.ClrType, navigation.Navigation);

                        if (comment is { })
                        {
                            navigation.Comment = comment;
                        }
                    }
                }
            }
        }

        /// <summary> Возвращает сборки с сущностями </summary>
        private static IEnumerable<Assembly> EnumerateAssemblies(IEnumerable<Type> types)
        {
            foreach (var entityType in types)
            {
                yield return entityType.Assembly;

                if (entityType.BaseType is null)
                {
                    continue;
                }

                var enumerable = EnumerateAssemblies(new[] { entityType.BaseType });

                foreach (var assembly in enumerable)
                {
                    yield return assembly;
                }
            }
        }

        private static CommentModel.CommentFromType? GetEntityComment(IReadOnlyDictionary<string, string?> commentDict, IEntityType type)
        {
            if (type.BaseType is not null)
            {
                return GetEntityComment(commentDict, type.BaseType);
            }

            if (commentDict.TryGetValue($"T:{type.ClrType.FullName}", out var comment) && !string.IsNullOrEmpty(comment))
            {
                return new CommentModel.CommentFromType(comment, type.ClrType);
            }

            return null;
        }

        private static string? GetPropertyComment(IReadOnlyDictionary<string, string?> commentDict, Type type, IPropertyBase property)
        {
            if (commentDict.TryGetValue($"P:{type.FullName}.{property.Name}", out var comment) && !string.IsNullOrEmpty(comment))
            {
                return comment;
            }

            return type.BaseType is null
                       ? null
                       : GetPropertyComment(commentDict, type.BaseType, property);
        }
    }
}