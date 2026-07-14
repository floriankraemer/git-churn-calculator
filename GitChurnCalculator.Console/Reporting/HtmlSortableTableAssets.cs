using System.Text;

namespace GitChurnCalculator.Console.Reporting;

internal static class HtmlSortableTableAssets
{
    public static void AppendStyles(StringBuilder sb)
    {
        sb.AppendLine("  <style>");
        sb.AppendLine("    th[data-sort-type] { cursor: pointer; user-select: none; }");
        sb.AppendLine("    th[data-sort-type] button { color: inherit; font: inherit; }");
        sb.AppendLine("    th[data-sort-direction=\"asc\"] button::after { content: \" \\25B2\"; }");
        sb.AppendLine("    th[data-sort-direction=\"desc\"] button::after { content: \" \\25BC\"; }");
        sb.AppendLine("    .report-filters { background: #fff; border: 1px solid #dee2e6; border-radius: .375rem; }");
        sb.AppendLine("    .report-filters .form-control { min-width: 12rem; }");
        sb.AppendLine("    .report-filters .filter-label { font-size: .85rem; color: #6c757d; }");
        sb.AppendLine("    @media (max-width: 767.98px) { .report-filters .form-control { min-width: 100%; } }");
        sb.AppendLine("  </style>");
    }

    public static void AppendScript(StringBuilder sb)
    {
        sb.AppendLine("  <script>");
        sb.AppendLine("    (() => {");
        sb.AppendLine("      const parseValue = (cell, type) => {");
        sb.AppendLine("        const raw = (cell?.dataset.sortValue ?? cell?.textContent ?? '').trim();");
        sb.AppendLine("        if (raw === '' || raw === '—') return null;");
        sb.AppendLine("        if (type === 'number') {");
        sb.AppendLine("          const value = Number(raw.replace(/,/g, ''));");
        sb.AppendLine("          return Number.isNaN(value) ? null : value;");
        sb.AppendLine("        }");
        sb.AppendLine("        if (type === 'date') {");
        sb.AppendLine("          const value = Date.parse(raw);");
        sb.AppendLine("          return Number.isNaN(value) ? null : value;");
        sb.AppendLine("        }");
        sb.AppendLine("        return raw.toLocaleLowerCase();");
        sb.AppendLine("      };");
        sb.AppendLine("      const parseThreshold = (input) => {");
        sb.AppendLine("        if (!input) return null;");
        sb.AppendLine("        const raw = input.value.trim();");
        sb.AppendLine("        if (raw === '') return null;");
        sb.AppendLine("        const value = Number(raw);");
        sb.AppendLine("        return Number.isFinite(value) ? value : null;");
        sb.AppendLine("      };");
        sb.AppendLine();
        sb.AppendLine("      const compareRows = (column, type, direction) => (left, right) => {");
        sb.AppendLine("        const leftValue = parseValue(left.children[column], type);");
        sb.AppendLine("        const rightValue = parseValue(right.children[column], type);");
        sb.AppendLine("        if (leftValue === null && rightValue === null) return 0;");
        sb.AppendLine("        if (leftValue === null) return 1;");
        sb.AppendLine("        if (rightValue === null) return -1;");
        sb.AppendLine("        const result = typeof leftValue === 'string'");
        sb.AppendLine("          ? leftValue.localeCompare(rightValue)");
        sb.AppendLine("          : leftValue - rightValue;");
        sb.AppendLine("        return direction === 'asc' ? result : -result;");
        sb.AppendLine("      };");
        sb.AppendLine();
        sb.AppendLine("      document.querySelectorAll('table[data-sortable=\"true\"]').forEach((table) => {");
        sb.AppendLine("        const scope = table.closest('[data-filter-scope]') ?? document;");
        sb.AppendLine("        const filters = scope.querySelector('[data-table-filters]');");
        sb.AppendLine("        const fileFilterInput = filters?.querySelector('[data-filter-file]') ?? null;");
        sb.AppendLine("        const coverageGreaterThanInput = filters?.querySelector('[data-filter-coverage-gt]') ?? null;");
        sb.AppendLine("        const coverageLessThanInput = filters?.querySelector('[data-filter-coverage-lt]') ?? null;");
        sb.AppendLine("        const applyFilters = () => {");
        sb.AppendLine("          const fileNeedle = (fileFilterInput?.value ?? '').trim().toLocaleLowerCase();");
        sb.AppendLine("          const coverageGreaterThan = parseThreshold(coverageGreaterThanInput);");
        sb.AppendLine("          const coverageLessThan = parseThreshold(coverageLessThanInput);");
        sb.AppendLine("          const hasCoverageFilter = coverageGreaterThan !== null || coverageLessThan !== null;");
        sb.AppendLine();
        sb.AppendLine("          Array.from(table.tBodies[0].rows).forEach((row) => {");
        sb.AppendLine("            const fileText = (row.children[0]?.textContent ?? '').toLocaleLowerCase();");
        sb.AppendLine("            const coverageValue = parseValue(row.children[4], 'number');");
        sb.AppendLine("            const fileMatches = fileNeedle === '' || fileText.includes(fileNeedle);");
        sb.AppendLine("            const coverageAbove = coverageGreaterThan === null");
        sb.AppendLine("              || (coverageValue !== null && coverageValue > coverageGreaterThan);");
        sb.AppendLine("            const coverageBelow = coverageLessThan === null");
        sb.AppendLine("              || (coverageValue !== null && coverageValue < coverageLessThan);");
        sb.AppendLine("            const coverageMatches = !hasCoverageFilter");
        sb.AppendLine("              ? true");
        sb.AppendLine("              : coverageValue !== null && coverageAbove && coverageBelow;");
        sb.AppendLine("            row.hidden = !(fileMatches && coverageMatches);");
        sb.AppendLine("          });");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        [fileFilterInput, coverageGreaterThanInput, coverageLessThanInput]");
        sb.AppendLine("          .filter((input) => input)");
        sb.AppendLine("          .forEach((input) => input.addEventListener('input', applyFilters));");
        sb.AppendLine("        applyFilters();");
        sb.AppendLine();
        sb.AppendLine("        table.querySelectorAll('thead th[data-sort-type]').forEach((header, column) => {");
        sb.AppendLine("          header.addEventListener('click', () => {");
        sb.AppendLine("            const direction = header.dataset.sortDirection === 'asc' ? 'desc' : 'asc';");
        sb.AppendLine("            header.parentElement.querySelectorAll('th[data-sort-direction]').forEach((sorted) => {");
        sb.AppendLine("              delete sorted.dataset.sortDirection;");
        sb.AppendLine("            });");
        sb.AppendLine("            header.dataset.sortDirection = direction;");
        sb.AppendLine("            const body = table.tBodies[0];");
        sb.AppendLine("            Array.from(body.rows)");
        sb.AppendLine("              .sort(compareRows(column, header.dataset.sortType, direction))");
        sb.AppendLine("              .forEach((row) => body.appendChild(row));");
        sb.AppendLine("          });");
        sb.AppendLine("        });");
        sb.AppendLine("      });");
        sb.AppendLine("    })();");
        sb.AppendLine("  </script>");
    }
}
