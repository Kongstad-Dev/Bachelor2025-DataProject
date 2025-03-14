﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Bach2025nortec.Migrations
{
    [DbContext(typeof(YourDbContext))]
    [Migration("20250313194907_InitialMigration")]
    partial class InitialMigration
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("Bach2025_nortec.Database.BankEntity", b =>
                {
                    b.Property<int>("bId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("name")
                        .HasColumnType("longtext");

                    b.HasKey("bId");

                    b.ToTable("bank", (string)null);
                });

            modelBuilder.Entity("Bach2025_nortec.Database.DataEntity", b =>
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

            modelBuilder.Entity("Bach2025_nortec.Database.Laundromat", b =>
                {
                    b.Property<string>("kId")
                        .HasColumnType("varchar(255)");

                    b.Property<int>("bId")
                        .HasColumnType("int");

                    b.Property<string>("bank")
                        .HasColumnType("longtext");

                    b.Property<string>("externalId")
                        .HasColumnType("longtext");

                    b.Property<DateTime?>("lastFetchDate")
                        .HasColumnType("datetime(6)");

                    b.Property<float>("latitude")
                        .HasColumnType("float");

                    b.Property<float>("longitude")
                        .HasColumnType("float");

                    b.Property<string>("name")
                        .HasColumnType("longtext");

                    b.Property<string>("zip")
                        .HasColumnType("longtext");

                    b.HasKey("kId");

                    b.HasIndex("bId");

                    b.ToTable("laundromat", (string)null);
                });

            modelBuilder.Entity("Bach2025_nortec.Database.TransactionEntity", b =>
                {
                    b.Property<string>("kId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("LaundromatId")
                        .HasColumnType("varchar(255)");

                    b.Property<int>("amount")
                        .HasColumnType("int");

                    b.Property<string>("currency")
                        .HasColumnType("longtext");

                    b.Property<DateTime>("date")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("debug")
                        .HasColumnType("longtext");

                    b.Property<int>("dirty")
                        .HasColumnType("int");

                    b.Property<int>("minuts")
                        .HasColumnType("int");

                    b.Property<int>("prewash")
                        .HasColumnType("int");

                    b.Property<int>("program")
                        .HasColumnType("int");

                    b.Property<int>("programType")
                        .HasColumnType("int");

                    b.Property<int>("rinse")
                        .HasColumnType("int");

                    b.Property<int>("seconds")
                        .HasColumnType("int");

                    b.Property<int>("soap")
                        .HasColumnType("int");

                    b.Property<int>("soapBrand")
                        .HasColumnType("int");

                    b.Property<int>("spin")
                        .HasColumnType("int");

                    b.Property<int>("temperature")
                        .HasColumnType("int");

                    b.Property<int>("transactionType")
                        .HasColumnType("int");

                    b.Property<string>("unitName")
                        .HasColumnType("longtext");

                    b.Property<int>("unitType")
                        .HasColumnType("int");

                    b.Property<string>("user")
                        .HasColumnType("longtext");

                    b.HasKey("kId");

                    b.HasIndex("LaundromatId");

                    b.ToTable("transaction", (string)null);
                });

            modelBuilder.Entity("Bach2025_nortec.Database.Laundromat", b =>
                {
                    b.HasOne("Bach2025_nortec.Database.BankEntity", "Bank")
                        .WithMany("Laundromats")
                        .HasForeignKey("bId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Bank");
                });

            modelBuilder.Entity("Bach2025_nortec.Database.TransactionEntity", b =>
                {
                    b.HasOne("Bach2025_nortec.Database.Laundromat", "Laundromat")
                        .WithMany("Transactions")
                        .HasForeignKey("LaundromatId");

                    b.Navigation("Laundromat");
                });

            modelBuilder.Entity("Bach2025_nortec.Database.BankEntity", b =>
                {
                    b.Navigation("Laundromats");
                });

            modelBuilder.Entity("Bach2025_nortec.Database.Laundromat", b =>
                {
                    b.Navigation("Transactions");
                });
#pragma warning restore 612, 618
        }
    }
}
