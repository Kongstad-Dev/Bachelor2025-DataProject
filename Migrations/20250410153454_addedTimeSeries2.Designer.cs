﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace BlazorTest.Migrations
{
    [DbContext(typeof(YourDbContext))]
    [Migration("20250410153454_addedTimeSeries2")]
    partial class addedTimeSeries2
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.15")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("BlazorTest.Database.BankEntity", b =>
                {
                    b.Property<int>("bankId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("name")
                        .HasColumnType("longtext");

                    b.HasKey("bankId");

                    b.ToTable("bank", (string)null);
                });

            modelBuilder.Entity("BlazorTest.Database.Laundromat", b =>
                {
                    b.Property<string>("kId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("bank")
                        .HasColumnType("longtext")
                        .HasAnnotation("Relational:JsonPropertyName", "bankName");

                    b.Property<int>("bankId")
                        .HasColumnType("int");

                    b.Property<string>("externalId")
                        .HasColumnType("longtext");

                    b.Property<DateTime?>("lastFetchDate")
                        .HasColumnType("datetime(6)");

                    b.Property<float>("latitude")
                        .HasColumnType("float");

                    b.Property<int>("locationId")
                        .HasColumnType("int");

                    b.Property<float>("longitude")
                        .HasColumnType("float");

                    b.Property<string>("name")
                        .HasColumnType("longtext");

                    b.Property<string>("zip")
                        .HasColumnType("longtext");

                    b.HasKey("kId");

                    b.HasIndex("bankId")
                        .HasDatabaseName("IX_Laundromat_BankId");

                    b.HasIndex("lastFetchDate")
                        .HasDatabaseName("IX_Laundromat_LastFetchDate");

                    b.HasIndex("latitude", "longitude")
                        .HasDatabaseName("IX_Laundromat_Coordinates");

                    b.ToTable("laundromat", (string)null);
                });

            modelBuilder.Entity("BlazorTest.Database.LaundromatStats", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("AvailableTimeSeriesData")
                        .HasColumnType("int");

                    b.Property<DateTime>("CalculatedAt")
                        .HasColumnType("datetime(6)");

                    b.Property<decimal>("DryerStartPrice")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("DryerTransactions")
                        .HasColumnType("int");

                    b.Property<DateTime>("EndDate")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("LaundromatId")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<string>("LaundromatName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("PeriodKey")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<int>("PeriodType")
                        .HasColumnType("int");

                    b.Property<string>("RevenueTimeSeriesData")
                        .IsRequired()
                        .HasColumnType("json");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("datetime(6)");

                    b.Property<decimal>("TotalRevenue")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("TotalTransactions")
                        .HasColumnType("int");

                    b.Property<string>("TransactionCountTimeSeriesData")
                        .IsRequired()
                        .HasColumnType("json");

                    b.Property<decimal>("WasherStartPrice")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("WashingMachineTransactions")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("LaundromatId")
                        .HasDatabaseName("IX_LaundromatStats_LaundromatId");

                    b.HasIndex("PeriodKey")
                        .HasDatabaseName("IX_LaundromatStats_PeriodKey");

                    b.HasIndex("PeriodType")
                        .HasDatabaseName("IX_LaundromatStats_PeriodType");

                    b.HasIndex("LaundromatId", "PeriodType", "PeriodKey")
                        .HasDatabaseName("IX_LaundromatStats_Composite");

                    b.ToTable("laundromat_stats", (string)null);
                });

            modelBuilder.Entity("BlazorTest.Database.TransactionEntity", b =>
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

                    b.HasIndex("LaundromatId")
                        .HasDatabaseName("IX_Transaction_LaundromatId");

                    b.HasIndex("amount")
                        .HasDatabaseName("IX_Transaction_Amount");

                    b.HasIndex("programType")
                        .HasDatabaseName("IX_Transaction_ProgramType");

                    b.HasIndex("soap")
                        .HasDatabaseName("IX_Transaction_Soap");

                    b.HasIndex("temperature")
                        .HasDatabaseName("IX_Transaction_Temperature");

                    b.HasIndex("unitType")
                        .HasDatabaseName("IX_Transaction_UnitType");

                    b.HasIndex("LaundromatId", "date")
                        .HasDatabaseName("IX_Transaction_LaundromatId_Date");

                    b.ToTable("transaction", (string)null);
                });

            modelBuilder.Entity("BlazorTest.Database.Laundromat", b =>
                {
                    b.HasOne("BlazorTest.Database.BankEntity", "Bank")
                        .WithMany("Laundromats")
                        .HasForeignKey("bankId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Bank");
                });

            modelBuilder.Entity("BlazorTest.Database.LaundromatStats", b =>
                {
                    b.HasOne("BlazorTest.Database.Laundromat", "Laundromat")
                        .WithMany()
                        .HasForeignKey("LaundromatId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Laundromat");
                });

            modelBuilder.Entity("BlazorTest.Database.TransactionEntity", b =>
                {
                    b.HasOne("BlazorTest.Database.Laundromat", "Laundromat")
                        .WithMany("Transactions")
                        .HasForeignKey("LaundromatId");

                    b.Navigation("Laundromat");
                });

            modelBuilder.Entity("BlazorTest.Database.BankEntity", b =>
                {
                    b.Navigation("Laundromats");
                });

            modelBuilder.Entity("BlazorTest.Database.Laundromat", b =>
                {
                    b.Navigation("Transactions");
                });
#pragma warning restore 612, 618
        }
    }
}
