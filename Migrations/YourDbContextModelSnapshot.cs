﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Bach2025nortec.Migrations
{
    [DbContext(typeof(YourDbContext))]
    partial class YourDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("Laundromat", b =>
                {
                    b.Property<int>("kId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("bank")
                        .HasColumnType("longtext");

                    b.Property<int>("externalId")
                        .HasColumnType("int");

                    b.Property<float>("latitude")
                        .HasColumnType("float");

                    b.Property<float>("longitude")
                        .HasColumnType("float");

                    b.Property<string>("name")
                        .HasColumnType("longtext");

                    b.Property<string>("zip")
                        .HasColumnType("longtext");

                    b.HasKey("kId");

                    b.ToTable("Laundromat");
                });
#pragma warning restore 612, 618
        }
    }
}
