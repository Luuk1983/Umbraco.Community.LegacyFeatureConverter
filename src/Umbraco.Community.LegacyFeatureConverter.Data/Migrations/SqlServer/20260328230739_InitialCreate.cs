using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.LegacyFeatureConverter.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegacyFeatureConverterHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConverterType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsTestRun = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalDocumentTypes = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalDataTypes = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalContentNodes = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SuccessCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FailureCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SkippedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Summary = table.Column<string>(type: "nvarchar(max)", maxLength: 2147483647, nullable: true),
                    SelectedDocumentTypes = table.Column<string>(type: "nvarchar(max)", maxLength: 2147483647, nullable: true),
                    PerformingUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyFeatureConverterHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegacyFeatureConverterQueue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerializedOptions = table.Column<string>(type: "nvarchar(max)", maxLength: 2147483647, nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConversionHistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyFeatureConverterQueue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegacyFeatureConverterLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversionHistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ItemKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", maxLength: 2147483647, nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", maxLength: 2147483647, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyFeatureConverterLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegacyFeatureConverterLog_LegacyFeatureConverterHistory_ConversionHistoryId",
                        column: x => x.ConversionHistoryId,
                        principalTable: "LegacyFeatureConverterHistory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyFeatureConverterLog_HistoryId",
                table: "LegacyFeatureConverterLog",
                column: "ConversionHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyFeatureConverterLog_Timestamp",
                table: "LegacyFeatureConverterLog",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyFeatureConverterQueue_QueuedAt",
                table: "LegacyFeatureConverterQueue",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyFeatureConverterQueue_Status",
                table: "LegacyFeatureConverterQueue",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyFeatureConverterLog");

            migrationBuilder.DropTable(
                name: "LegacyFeatureConverterQueue");

            migrationBuilder.DropTable(
                name: "LegacyFeatureConverterHistory");
        }
    }
}
