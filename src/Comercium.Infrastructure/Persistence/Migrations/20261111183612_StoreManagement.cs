using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comercium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Roles SET Permissions = Permissions | 7680 WHERE Name = 'Propietaria';");
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Sales",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Sales",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "SaleLines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OpenedById = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Opening = table.Column<long>(type: "INTEGER", nullable: false),
                    ClosedById = table.Column<int>(type: "INTEGER", nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Counted = table.Column<long>(type: "INTEGER", nullable: true),
                    ExpectedAtClose = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSessions", x => x.Id);
                    table.CheckConstraint("CK_CashSession_Amounts", "Opening >= 0 AND (Counted IS NULL OR Counted >= 0)");
                    table.ForeignKey(
                        name: "FK_CashSessions_Users_ClosedById",
                        column: x => x.ClosedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashSessions_Users_OpenedById",
                        column: x => x.OpenedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SearchName = table.Column<string>(type: "TEXT", nullable: false),
                    Document = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: false),
                    Wholesale = table.Column<bool>(type: "INTEGER", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Mission = table.Column<string>(type: "TEXT", nullable: false),
                    Vision = table.Column<string>(type: "TEXT", nullable: false),
                    Values = table.Column<string>(type: "TEXT", nullable: false),
                    Strengths = table.Column<string>(type: "TEXT", nullable: false),
                    Opportunities = table.Column<string>(type: "TEXT", nullable: false),
                    Weaknesses = table.Column<string>(type: "TEXT", nullable: false),
                    Threats = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CashSessionId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    SourceId = table.Column<long>(type: "INTEGER", nullable: true),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashEntries", x => x.Id);
                    table.CheckConstraint("CK_CashEntry_Amount", "Amount <> 0");
                    table.ForeignKey(
                        name: "FK_CashEntries_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Objectives",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StrategyPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Strategy = table.Column<string>(type: "TEXT", nullable: false),
                    Evaluation = table.Column<string>(type: "TEXT", nullable: false),
                    Indicator = table.Column<string>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    Target = table.Column<decimal>(type: "TEXT", nullable: false),
                    Due = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Completed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Objectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Objectives_Plans_StrategyPlanId",
                        column: x => x.StrategyPlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IndicatorReading",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ObjectiveId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndicatorReading", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndicatorReading_Objectives_ObjectiveId",
                        column: x => x.ObjectiveId,
                        principalTable: "Objectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IndicatorReading_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_CategoryId",
                table: "SaleLines",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CashEntries_AtUtc",
                table: "CashEntries",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CashEntries_CashSessionId",
                table: "CashEntries",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CashEntries_Kind_SourceId",
                table: "CashEntries",
                columns: new[] { "Kind", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashEntries_OperationId",
                table: "CashEntries",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashEntries_UserId",
                table: "CashEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_ClosedById",
                table: "CashSessions",
                column: "ClosedById");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_OpenedById",
                table: "CashSessions",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Document",
                table: "Customers",
                column: "Document",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_SearchName",
                table: "Customers",
                column: "SearchName");

            migrationBuilder.CreateIndex(
                name: "IX_IndicatorReading_ObjectiveId",
                table: "IndicatorReading",
                column: "ObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_IndicatorReading_UserId",
                table: "IndicatorReading",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Objectives_StrategyPlanId",
                table: "Objectives",
                column: "StrategyPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleLines_Categories_CategoryId",
                table: "SaleLines",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Customers_CustomerId",
                table: "Sales",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Roles SET Permissions = Permissions & ~7680 WHERE Name = 'Propietaria';");
            migrationBuilder.DropForeignKey(
                name: "FK_SaleLines_Categories_CategoryId",
                table: "SaleLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Customers_CustomerId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "CashEntries");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "IndicatorReading");

            migrationBuilder.DropTable(
                name: "CashSessions");

            migrationBuilder.DropTable(
                name: "Objectives");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SaleLines_CategoryId",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "SaleLines");
        }
    }
}
