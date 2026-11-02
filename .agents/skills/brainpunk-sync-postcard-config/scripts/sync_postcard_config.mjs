import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const DEFAULT_REPO_ROOT = "D:\\GitStore\\BrinPunk\\BrainPunk";
const DEFAULT_WORKBOOK = "Luban/Configs/Datas/#PostcardConfig.xlsx";
const DEFAULT_PREFAB_FOLDER = "Assets/Game/UI/Postcard/Prefabs/Pages";

function parseArgs(argv) {
  const args = {};
  for (let index = 0; index < argv.length; index += 1) {
    const key = argv[index];
    if (!key.startsWith("--")) throw new Error(`Unexpected argument: ${key}`);
    const value = argv[index + 1];
    if (!value || value.startsWith("--")) throw new Error(`Missing value for ${key}`);
    args[key.slice(2)] = value;
    index += 1;
  }
  return args;
}

function toAbsolute(root, value) {
  return path.isAbsolute(value) ? value : path.resolve(root, value);
}

function normalize(value) {
  return String(value ?? "").trim().toLowerCase();
}

function canonicalSnapshot(range) {
  const formulas = range.formulas;
  const values = range.values.map((row, rowIndex) => row.map((value, columnIndex) => (
    String(formulas[rowIndex][columnIndex] ?? "").length > 0 ? null : value
  )));
  return { values, formulas };
}

function snapshot(range) {
  return JSON.stringify(canonicalSnapshot(range));
}

function describeSnapshotDiff(beforeSnapshot, afterRange) {
  const before = JSON.parse(beforeSnapshot);
  const after = canonicalSnapshot(afterRange);
  const differences = [];
  for (const property of ["values", "formulas"]) {
    for (let row = 0; row < before[property].length; row += 1) {
      for (let column = 0; column < before[property][row].length; column += 1) {
        if (JSON.stringify(before[property][row][column]) !== JSON.stringify(after[property][row][column])) {
          differences.push({ property, row: row + 1, column: column + 1, before: before[property][row][column], after: after[property][row][column] });
        }
      }
    }
  }
  return differences.slice(0, 20);
}

const args = parseArgs(process.argv.slice(2));
const mode = args.mode ?? "analyze";
if (!new Set(["analyze", "write"]).has(mode)) throw new Error(`Unsupported mode: ${mode}`);

const repoRoot = path.resolve(args["repo-root"] ?? DEFAULT_REPO_ROOT);
const workbookPath = toAbsolute(repoRoot, args.workbook ?? DEFAULT_WORKBOOK);
const prefabFolder = path.join(repoRoot, DEFAULT_PREFAB_FOLDER);
const outputPath = args.output ? path.resolve(args.output) : null;
const previewPath = args.preview ? path.resolve(args.preview) : null;
if (mode === "write" && (!outputPath || !previewPath)) {
  throw new Error("Write mode requires --output and --preview.");
}
if (outputPath && path.resolve(outputPath) === path.resolve(workbookPath)) {
  throw new Error("The script never overwrites the source workbook directly.");
}

await fs.access(workbookPath);
await fs.access(prefabFolder);

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(workbookPath));
if (workbook.worksheets.items.length !== 1) throw new Error("Expected exactly one worksheet.");
const sheet = workbook.worksheets.getItemAt(0);
const usedRange = sheet.getUsedRange();
const originalAddress = usedRange.address;
const addressMatch = originalAddress.match(/^A1:M(\d+)$/i);
if (!addressMatch) throw new Error(`Unexpected used range: ${originalAddress}`);
if (String(sheet.getRange("B1").values[0][0]) !== "id") throw new Error("Expected id in B1.");
if (String(sheet.getRange("G1").values[0][0]) !== "themeName") throw new Error("Expected themeName in G1.");

const originalLastRow = Number(addressMatch[1]);
if (originalLastRow < 6) throw new Error(`Unexpected data row count: ${originalLastRow}`);
const originalSnapshot = snapshot(usedRange);
const dataValues = sheet.getRange(`A6:M${originalLastRow}`).values;

const existingPrefabPaths = new Set(dataValues.map((row) => normalize(row[2])).filter(Boolean));
const rowsById = new Map();
const rowsByThemeName = new Map();
for (let index = 0; index < dataValues.length; index += 1) {
  const rowNumber = index + 6;
  const id = String(dataValues[index][1] ?? "").trim();
  const themeName = String(dataValues[index][6] ?? "").trim();
  if (id) {
    if (rowsById.has(id)) throw new Error(`Duplicate existing id ${id} at rows ${rowsById.get(id)} and ${rowNumber}.`);
    rowsById.set(id, rowNumber);
  }
  if (themeName) {
    if (rowsByThemeName.has(themeName)) {
      throw new Error(`Duplicate existing themeName ${themeName} at rows ${rowsByThemeName.get(themeName)} and ${rowNumber}.`);
    }
    rowsByThemeName.set(themeName, rowNumber);
  }
}

const prefabNames = (await fs.readdir(prefabFolder))
  .filter((fileName) => /^Postcard_.+\.prefab$/i.test(fileName))
  .sort((left, right) => left.localeCompare(right, "en", { sensitivity: "base" }));
const additions = [];
const inferredIds = new Map();

for (const fileName of prefabNames) {
  const prefabPathKey = fileName.slice(0, -".prefab".length);
  if (existingPrefabPaths.has(normalize(prefabPathKey))) continue;

  const contents = await fs.readFile(path.join(prefabFolder, fileName), "utf8");
  const indices = new Set(
    [...contents.matchAll(/^\s*m_Name:\s*UI_mingxinpian_img_juese(\d+)_01\s*$/gim)]
      .map((match) => Number(match[1])),
  );
  if (indices.size !== 1) {
    throw new Error(`${fileName} must contain exactly one unique juese index, found: ${[...indices].join(", ") || "none"}.`);
  }

  const themeIndex = [...indices][0];
  const id = String(30000 + themeIndex);
  const themeName = `U_Diary_title_${id}`;
  if (inferredIds.has(id)) {
    throw new Error(`Prefabs ${inferredIds.get(id)} and ${fileName} infer the same id ${id}.`);
  }
  inferredIds.set(id, fileName);

  const idRow = rowsById.get(id);
  const themeRow = rowsByThemeName.get(themeName);
  if (idRow || themeRow) {
    if (idRow && themeRow && idRow === themeRow) continue;
    throw new Error(`${fileName} conflicts with existing id/themeName rows: id row=${idRow ?? "none"}, themeName row=${themeRow ?? "none"}.`);
  }

  additions.push({ prefab: prefabPathKey, themeIndex, id: Number(id), themeName });
}
additions.sort((left, right) => left.id - right.id);

const analysis = {
  mode,
  repoRoot,
  workbookPath,
  prefabFolder,
  originalAddress,
  count: additions.length,
  additions,
};

if (mode === "analyze" || additions.length === 0) {
  console.log(JSON.stringify(analysis, null, 2));
  process.exit(0);
}

const firstNewRow = originalLastRow + 1;
const lastNewRow = firstNewRow + additions.length - 1;
for (let row = firstNewRow; row <= lastNewRow; row += 1) {
  sheet.getRange(`A${row}:M${row}`).copyFrom(sheet.getRange(`A${originalLastRow}:M${originalLastRow}`), "all");
}
sheet.getRange(`A${firstNewRow}:M${lastNewRow}`).clear({ applyTo: "contents" });
sheet.getRange(`B${firstNewRow}:B${lastNewRow}`).values = additions.map((item) => [item.id]);
sheet.getRange(`G${firstNewRow}:G${lastNewRow}`).values = additions.map((item) => [item.themeName]);
sheet.getRange(`B${firstNewRow}:B${lastNewRow}`).format.fill = "#FFFF00";
sheet.getRange(`G${firstNewRow}:G${lastNewRow}`).format.fill = "#FFFF00";

if (snapshot(sheet.getRange(originalAddress)) !== originalSnapshot) {
  throw new Error(`Original workbook values or formulas changed before export: ${JSON.stringify(describeSnapshotDiff(originalSnapshot, sheet.getRange(originalAddress)))}`);
}

await fs.mkdir(path.dirname(outputPath), { recursive: true });
await fs.mkdir(path.dirname(previewPath), { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);

const checkBook = await SpreadsheetFile.importXlsx(await FileBlob.load(outputPath));
if (checkBook.worksheets.items.length !== 1) throw new Error("Export changed worksheet count.");
const checkSheet = checkBook.worksheets.getItemAt(0);
if (snapshot(checkSheet.getRange(originalAddress)) !== originalSnapshot) {
  throw new Error(`Export changed original workbook values or formulas: ${JSON.stringify(describeSnapshotDiff(originalSnapshot, checkSheet.getRange(originalAddress)))}`);
}

const expectedRows = additions.map((item) => [
  null,
  item.id,
  null,
  null,
  null,
  null,
  item.themeName,
  null,
  null,
  null,
  null,
  null,
  null,
]);
const actualRows = checkSheet.getRange(`A${firstNewRow}:M${lastNewRow}`).values;
if (JSON.stringify(actualRows) !== JSON.stringify(expectedRows)) {
  throw new Error("Exported additions contain unexpected values outside id/themeName.");
}
const addedFormulas = checkSheet.getRange(`A${firstNewRow}:M${lastNewRow}`).formulas;
if (addedFormulas.some((row) => row.some((formula) => String(formula ?? "").length > 0))) {
  throw new Error("Exported additions contain unexpected formulas.");
}

const errors = await checkBook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 100 },
  summary: "Postcard config formula error scan",
});
if (!errors.ndjson.includes("matched 0 entries")) throw new Error(`Formula errors found: ${errors.ndjson}`);

const preview = await checkBook.render({
  sheetName: checkSheet.name,
  range: `A1:M${lastNewRow}`,
  scale: 1.5,
  format: "png",
});
await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));

console.log(JSON.stringify({
  ...analysis,
  outputPath,
  previewPath,
  newRange: `A${firstNewRow}:M${lastNewRow}`,
  idRange: `B${firstNewRow}:B${lastNewRow}`,
  themeNameRange: `G${firstNewRow}:G${lastNewRow}`,
  originalValuesAndFormulasPreserved: true,
  formulaErrors: 0,
}, null, 2));
