using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HEMedical.Hospital.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ClinicalMeasurementSequence");

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sex = table.Column<int>(type: "int", nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodPressure",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR [ClinicalMeasurementSequence]"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InterpretationCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InterpretationSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Systolic = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Diastolic = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodPressure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodPressure_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hb1Ac",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR [ClinicalMeasurementSequence]"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InterpretationCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InterpretationSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hb1Ac", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hb1Ac_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BloodPressure_PatientId",
                table: "BloodPressure",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Hb1Ac_PatientId",
                table: "Hb1Ac",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BloodPressure");

            migrationBuilder.DropTable(
                name: "Hb1Ac");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropSequence(
                name: "ClinicalMeasurementSequence");
        }
    }
}
