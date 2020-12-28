﻿// <auto-generated />
using System;
using GraphQL.WebApi.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace GraphQL.WebApi.Migrations
{
    [DbContext(typeof(DatabaseContext))]
    [Migration("20201228134048_UniqNames")]
    partial class UniqNames
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .HasAnnotation("ProductVersion", "3.1.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("GraphQL.WebApi.Models.City", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<int>("country_id")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("creation_date")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp without time zone")
                        .HasDefaultValueSql("(now())");

                    b.Property<string>("name")
                        .HasColumnType("text");

                    b.Property<int?>("population")
                        .HasColumnType("integer");

                    b.HasKey("id");

                    b.HasIndex("country_id");

                    b.HasIndex("name")
                        .IsUnique();

                    b.ToTable("city","business");
                });

            modelBuilder.Entity("GraphQL.WebApi.Models.Country", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<string>("continent")
                        .HasColumnType("text");

                    b.Property<DateTime?>("creation_date")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp without time zone")
                        .HasDefaultValueSql("(now())");

                    b.Property<string>("name")
                        .HasColumnType("text");

                    b.HasKey("id");

                    b.HasIndex("name")
                        .IsUnique();

                    b.ToTable("country","business");
                });

            modelBuilder.Entity("GraphQL.WebApi.Models.City", b =>
                {
                    b.HasOne("GraphQL.WebApi.Models.Country", "Country")
                        .WithMany()
                        .HasForeignKey("country_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
