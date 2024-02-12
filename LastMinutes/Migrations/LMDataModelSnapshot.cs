﻿// <auto-generated />
using System;
using LastMinutes.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace LastMinutes.Migrations
{
    [DbContext(typeof(LMData))]
    partial class LMDataModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.26")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("LastMinutes.Models.LMData.Queue", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"), 1L, 1);

                    b.Property<DateTime>("Created_On")
                        .HasColumnType("datetime2");

                    b.Property<int>("Mode")
                        .HasColumnType("int");

                    b.Property<DateTime>("Updated_On")
                        .HasColumnType("datetime2");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("LM_Queue");
                });

            modelBuilder.Entity("LastMinutes.Models.LMData.Results", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"), 1L, 1);

                    b.Property<string>("AllScrobbles")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("BadScrobbles")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("Created_On")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("FromWhen")
                        .HasColumnType("datetime2");

                    b.Property<int>("Mode")
                        .HasColumnType("int");

                    b.Property<DateTime>("ToWhen")
                        .HasColumnType("datetime2");

                    b.Property<long>("TotalPlaytime")
                        .HasColumnType("bigint");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("LM_Results");
                });

            modelBuilder.Entity("LastMinutes.Models.LMData.Tracks", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"), 1L, 1);

                    b.Property<string>("Artist")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("Date_Added")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("Last_Used")
                        .HasColumnType("datetime2");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Runtime")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("LM_Tracks");
                });
#pragma warning restore 612, 618
        }
    }
}
