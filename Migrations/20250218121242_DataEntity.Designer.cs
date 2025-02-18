﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Bach2025nortec.Migrations
{
    [DbContext(typeof(YourDbContext))]
    [Migration("20250218121242_DataEntity")]
    partial class DataEntity
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("Bach2025_nortecnortec.Database.DataEntity", b =>
                {
                    b.Property<int>("kId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("amount")
                        .HasColumnType("int");

                    b.Property<string>("currency")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("debug")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("dirty")
                        .HasColumnType("int");

                    b.Property<int>("externalId")
                        .HasColumnType("int");

                    b.Property<int>("minuts")
                        .HasColumnType("int");

                    b.Property<int>("prewash")
                        .HasColumnType("int");

                    b.Property<int>("program")
                        .HasColumnType("int");

                    b.Property<int>("programtype")
                        .HasColumnType("int");

                    b.Property<int>("rinse")
                        .HasColumnType("int");

                    b.Property<int>("seconds")
                        .HasColumnType("int");

                    b.Property<int>("soapBrand")
                        .HasColumnType("int");

                    b.Property<int>("spin")
                        .HasColumnType("int");

                    b.Property<int>("temperature")
                        .HasColumnType("int");

                    b.Property<int>("transactionsType")
                        .HasColumnType("int");

                    b.Property<string>("unitName")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("unittype")
                        .HasColumnType("int");

                    b.Property<string>("user")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("kId");

                    b.ToTable("DataEntities");
                });

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
