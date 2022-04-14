using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EFCore.DbComments
{
    /// <summary>Comments model</summary>
    public class CommentModel
    {
        /// <summary>Comments model</summary>
        internal CommentModel(ModelBuilder modelBuilder, string discriminatorComment)
        {
            ModelBuilder = modelBuilder;
            DiscriminatorComment = discriminatorComment;
            EntityComments = modelBuilder.Model.GetEntityTypes().Select(et => new EntityComment(et)).ToImmutableArray();
        }

        internal ModelBuilder ModelBuilder { get; }

        /// <summary> Entities with comments</summary>
        internal IReadOnlyCollection<EntityComment> EntityComments { get; }

        /// <summary> Comment for discriminator </summary>
        internal string DiscriminatorComment { get; }

        /// <summary> Comment for entity </summary>
        internal class EntityComment
        {
            /// <summary> Comment for entity </summary>
            public EntityComment(IMutableEntityType entityType)
            {
                EntityType = entityType;
                EntityProperties = entityType.GetProperties().Select(p => new PropertyComment(p)).ToImmutableArray();
                EntityNavigations = entityType.GetNavigations().Select(p => new NavigationComment(p)).ToImmutableArray();
            }

            /// <summary> Comment </summary>
            public string? Comment => InternalComment?.Comment;

            /// <summary> InternalComment </summary>
            internal CommentFromType? InternalComment { get; set; }

            /// <summary> Type </summary>
            public IMutableEntityType EntityType { get; }

            /// <summary> Properties </summary>
            public IReadOnlyCollection<PropertyComment> EntityProperties { get; }

            /// <summary> Navigations </summary>
            public IReadOnlyCollection<NavigationComment> EntityNavigations { get; }
        }

        /// <summary> Comment with holder type </summary>
        internal readonly struct CommentFromType
        {
            /// <summary> Comment with holder type </summary>
            public CommentFromType(string comment, Type holderType)
            {
                Comment = comment;
                HolderType = holderType;
            }

            /// <summary> Comment </summary>
            public string Comment { get; }

            /// <summary> Holder type </summary>
            public Type HolderType { get; }
        }

        /// <summary> Comment for property </summary>
        internal class PropertyComment
        {
            /// <summary> Comment for property </summary>
            public PropertyComment(IMutableProperty property)
            {
                Property = property;
            }

            /// <summary> Comment </summary>
            public string? Comment { get; set; }

            /// <summary> Property </summary>
            public IMutableProperty Property { get; }
        }

        /// <summary> Comment for navigation </summary>
        internal class NavigationComment
        {
            /// <summary> Comment for navigation </summary>
            public NavigationComment(IMutableNavigation navigation)
            {
                Navigation = navigation;
            }

            /// <summary> Comment </summary>
            public string? Comment { get; set; }

            /// <summary> Navigation </summary>
            public IMutableNavigation Navigation { get; }
        }

        /// <summary> Comments the model </summary>
        public static void CommentModelFromXml(ModelBuilder modelBuilder)
        {
           new CommentModelBuilder().WithXmlComments().Build(modelBuilder).AddCommentsToModel();
        }

        /// <summary> Comments the model </summary>
        public static void CommentModelFromDescriptionAttr(ModelBuilder modelBuilder)
        {
            new CommentModelBuilder().WithDescriptionComments().Build(modelBuilder).AddCommentsToModel();
        }

        /// <summary> Comments on the model </summary>
        private void AddCommentsToModel()
        {
            foreach (var entity in EntityComments)
            {
                if (entity.EntityType.FindPrimaryKey() is null && ((IConventionEntityType)entity.EntityType).IsKeyless == false)
                {
                    continue;
                }

                if (entity.EntityType.IsOwned())
                {
                    var entityTypeBuilder = ModelBuilder.Entity(entity.EntityType.FindOwnership()!.PrincipalEntityType.ClrType);
                    var ownerEntityComment = EntityComments.SingleOrDefault(x => x.EntityType == entityTypeBuilder.Metadata);

                    CommentOnColumns(entity, property => CommentOwned(entityTypeBuilder, entity, property, ownerEntityComment));
                }
                else
                {
                    // comments on table
                    var entityTypeBuilder = ModelBuilder.Entity(entity.EntityType.ClrType);

                    if (string.IsNullOrEmpty(entity.EntityType.GetComment()) && !string.IsNullOrEmpty(entity.Comment))
                    {
                        entityTypeBuilder.HasComment(entity.Comment);
                    }

                    CommentDiscriminator(entity.EntityType, entityTypeBuilder);

                    CommentOnColumns(
                        entity, property =>
                        {
                            var propertyBuilder = entityTypeBuilder.Property(property.Property.Name);

                            if (string.IsNullOrEmpty(property.Property.GetComment()) && !string.IsNullOrEmpty(property.Comment))
                            {
                                propertyBuilder.HasComment(property.Comment);
                            }
                        });
                }
            }
        }

        private static void CommentOnColumns(EntityComment entity, Action<PropertyComment> commentAction)
        {
            foreach (var property in entity.EntityProperties)
            {
                if (string.IsNullOrEmpty(property.Comment))
                {
                    continue;
                }

                commentAction(property);
            }
        }

        private static void CommentOwned(
            EntityTypeBuilder entityTypeBuilder, EntityComment entity, PropertyComment property,
            EntityComment? ownerEntityComment)
        {
            foreach (var navigation in entityTypeBuilder.Metadata.GetNavigations().Where(x => x.TargetEntityType == entity.EntityType))
            {
                PropertyBuilder? ownedPropertyBuilder;

                if (navigation.IsCollection)
                {
                    ownedPropertyBuilder = entityTypeBuilder.OwnsMany(entity.EntityType.ClrType, navigation.Name)
                        .Property(property.Property.Name);
                }
                else
                {
                    ownedPropertyBuilder = entityTypeBuilder.OwnsOne(entity.EntityType.ClrType, navigation.Name)
                        .Property(property.Property.Name);
                }

                var navigationComment = ownerEntityComment?.EntityNavigations.SingleOrDefault(x => x.Navigation == navigation)?.Comment;
                var comment = navigationComment is null ? property.Comment : $"{navigationComment}:{property.Comment}";

                ownedPropertyBuilder.HasComment(comment);
            }
        }

        private void CommentDiscriminator(IMutableEntityType entityType, EntityTypeBuilder entityTypeBuilder)
        {
            var discriminatorColumn = entityType.BaseType?.GetDiscriminatorPropertyName();

            if (discriminatorColumn is null)
            {
                return;
            }

            var propertyTypeBuilder = entityTypeBuilder.Property(discriminatorColumn);

            propertyTypeBuilder.HasComment(DiscriminatorComment);
        }
    }
}