using System;
using Microsoft.EntityFrameworkCore;

namespace EFCore.DbComments
{
    /// <summary> Extension methods for  <see cref="ModelBuilder"/> </summary>
    public static class ModelBuilderExtensions
    {
        /// <summary> Comments the model with xml documentation usage</summary>
        public static void CommentModelFromXml(this ModelBuilder modelBuilder)
        {
            if (modelBuilder is null)
            {
                throw new ArgumentNullException(nameof(modelBuilder));
            }

            CommentModel.CommentModelFromXml(modelBuilder);
        }

        /// <summary> Comments the model with <see cref="System.ComponentModel.DescriptionAttribute"/> usage</summary>
        public static void CommentModelFromDescriptionAttr(this ModelBuilder modelBuilder)
        {
            if (modelBuilder is null)
            {
                throw new ArgumentNullException(nameof(modelBuilder));
            }

            CommentModel.CommentModelFromDescriptionAttr(modelBuilder);
        }
    }
}