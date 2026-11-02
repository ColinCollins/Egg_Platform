import fs from "node:fs/promises";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const DEFAULT_REPO_ROOT = "D:\\GitStore\\BrinPunk\\BrainPunk";

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i += 1) {
    const key = argv[i];
    if (!key.startsWith("--")) throw new Error(`Unexpected argument: ${key}`);
    const value = argv[i + 1];
    if (!value || value.startsWith("--")) throw new Error(`Missing value for ${key}`);
    args[key.slice(2)] = value;
    i += 1;
  }
  return args;
}

function toAbsolute(root, value) {
  return path.isAbsolute(value) ? value : path.resolve(root, value);
}

const args = parseArgs(process.argv.slice(2));
const mode = args.mode ?? "analyze";
if (!new Set(["analyze", "write"]).has(mode)) throw new Error(`Unsupported mode: ${mode}`);

const repoRoot = path.resolve(args["repo-root"] ?? DEFAULT_REPO_ROOT);
const target = args.target ?? "skin";
if (!["skin", "shop"].includes(target)) throw new Error("Unknown target");
const workbookPath = toAbsolute(repoRoot, args.workbook ?? `Luban/Configs/Datas/${target === "shop" ? "SkinShop" : "Skin"}.xlsx`);
const outputPath = args.output ? path.resolve(args.output) : null;
const previewPath = args.preview ? path.resolve(args.preview) : null;
if (mode === "write" && (!outputPath || !previewPath)) {
  throw new Error("Write mode requires --output and --preview.");
}
if (outputPath && path.resolve(outputPath) === path.resolve(workbookPath)) {
  throw new Error("The script never overwrites the source workbook directly.");
}

const categories = [
  {
    folder: "Assets/Game/ArtSrc/Skins/Back",
    pattern: /^Sp_bs_img_(\d{3})\.png$/i,
    resourcePrefix: "Sp_bs_img_",
    idBase: 201000,
    namePrefix: "U_Skin_Name_Back_",
    skinPart: "Back",
  },
  {
    folder: "Assets/Game/ArtSrc/Skins/Face",
    pattern: /^Sp_zs_img_(\d{3})\.png$/i,
    resourcePrefix: "Sp_zs_img_",
    idBase: 202000,
    namePrefix: "U_Skin_Name_Eyes_",
    skinPart: "Face",
  },
  {
    folder: "Assets/Game/ArtSrc/Skins/Clothes",
    pattern: /^Sp_zs_fz_img_(\d{3})\.png$/i,
    resourcePrefix: "Sp_zs_fz_img_",
    idBase: 203000,
    namePrefix: "U_Skin_Name_Outfit_",
    skinPart: "Body",
  },
];

for (const requiredPath of [workbookPath, ...categories.map((item) => path.join(repoRoot, item.folder))]) {
  await fs.access(requiredPath);
}

const input = await FileBlob.load(workbookPath);
const workbook = await SpreadsheetFile.importXlsx(input);
// 独立读取 OOXML，避免同一导入器将空共享字符串误读为索引数字而自校验通过。
const readRaw = (file) => JSON.parse(execFileSync(args.python ?? "python", [fileURLToPath(new URL('./read_xlsx_cells.py', import.meta.url)), file], {encoding:'utf8'}));
const rawOriginal = readRaw(workbookPath);
for (const s of workbook.worksheets.items) {
  for (const [address, cell] of Object.entries(rawOriginal[s.name])) {
    if (cell.kind === 'blank' && !cell.formula) s.getRange(address).values = [[null]];
  }
}
// 按工作表名称定位；其它工作表只读取并验证，不参与追加。
const originals = workbook.worksheets.items.map(s => ({name:s.name,address:s.getUsedRange().address,values:JSON.stringify(s.getUsedRange().values),formulas:JSON.stringify(s.getUsedRange().formulas)}));
const sheet = workbook.worksheets.getItem(target === "shop" ? "actorSkinShop" : "actorSkin");
const usedRange = sheet.getUsedRange();
const originalAddress = usedRange.address;
const originalValuesJson = JSON.stringify(usedRange.values);
const lastColumn = target === "shop" ? "F" : "M";
const addressMatch = originalAddress.match(new RegExp(`:${lastColumn}(\\d+)$`, "i"));
if (!addressMatch || !originalAddress.toUpperCase().startsWith("A1:")) {
  throw new Error(`Unexpected used range: ${originalAddress}`);
}
const originalLastRow = Number(addressMatch[1]);
if (originalLastRow < 6) throw new Error(`Unexpected data row count: ${originalLastRow}`);

const dataValues = sheet.getRange(`A6:${lastColumn}${originalLastRow}`).values;
for (const column of (target === "shop" ? [1,2] : [1,2,5,6])) {
  const seen = new Set();
  for (const row of dataValues) {
    const value = row[column];
    if (value == null || value === "") continue;
    if (seen.has(String(value))) throw new Error(`Duplicate existing value: ${value}`);
    seen.add(String(value));
  }
}
const existingIds = new Set(dataValues.map((row) => String(row[1] ?? "")).filter(Boolean));
const existingNames = new Set(dataValues.map((row) => String(row[2] ?? "")).filter(Boolean));
const existingKeys = new Set(
  dataValues.flatMap((row) => [row[5], row[6]]).filter((value) => typeof value === "string" && value.length > 0),
);

const additions = [];
if (target === "shop") {
  // 商店只同步角色部件；不将此偏移规则应用于铅笔皮肤。
  const sourceBook = await SpreadsheetFile.importXlsx(await FileBlob.load(path.join(repoRoot, "Luban/Configs/Datas/Skin.xlsx")));
  const sourceRows = sourceBook.worksheets.getItem("actorSkin").getUsedRange().values.slice(5);
  const sourceIds = new Set();
  for (const row of sourceRows) {
    if (row[1] == null) continue;
    if (sourceIds.has(String(row[1]))) throw new Error(`Duplicate Skin id: ${row[1]}`);
    sourceIds.add(String(row[1]));
  }
  for (const row of dataValues) {
    if (!sourceIds.has(String(row[2])) || Number(row[1]) !== Number(row[2]) + 10000) throw new Error(`Unexpected shop mapping: ${row[1]}/${row[2]}`);
  }
  for (const row of sourceRows) {
    if (!['Back','Face','Body'].includes(row[4]) || existingNames.has(String(row[1]))) continue;
    const id = Number(row[1]) + 10000;
    if (!Number.isSafeInteger(id) || existingIds.has(String(id))) throw new Error(`Shop id conflict: ${id}`);
    const pattern = {Back:/^Sp_bs_img_\d{3}$/,Face:/^Sp_zs_img_\d{3}$/,Body:/^Sp_zs_fz_img_\d{3}$/}[row[4]];
    if (!pattern.test(row[5]) || row[5] !== row[6]) throw new Error(`Unexpected resource mapping: ${row[1]}`);
    await fs.access(path.join(repoRoot, 'Assets/Game/ArtSrc/Skins/UI', `${row[5]}.png`));
    await fs.access(path.join(repoRoot, 'Assets/Game/Prefabs/Skins', `${row[6]}.prefab`));
    additions.push({id,skinId:Number(row[1])});
  }
} else {
for (const category of categories) {
  const folderPath = path.join(repoRoot, category.folder);
  for (const fileName of (await fs.readdir(folderPath)).sort()) {
    const match = category.pattern.exec(fileName);
    if (!match) { if (/\.png$/i.test(fileName)) throw new Error(`Unknown PNG: ${fileName}`); continue; }
    const index = Number(match[1]);
    const resourceKey = `${category.resourcePrefix}${String(index).padStart(3, "0")}`;
    if (existingKeys.has(resourceKey)) continue;

    const id = String(category.idBase + index);
    const skinName = `${category.namePrefix}${String(index).padStart(2, "0")}`;
    if (existingIds.has(id)) throw new Error(`GoodsID conflict: ${id}`);
    if (existingNames.has(skinName)) throw new Error(`skinName conflict: ${skinName}`);

    const uiIconPath = path.join(repoRoot, "Assets/Game/ArtSrc/Skins/UI", `${resourceKey}.png`);
    const prefabPath = path.join(repoRoot, "Assets/Game/Prefabs/Skins", `${resourceKey}.prefab`);
    await fs.access(uiIconPath);
    await fs.access(prefabPath);
    additions.push({ id: Number(id), skinName, skinPart: category.skinPart, resourceKey });
  }
}
}
additions.sort((a, b) => a.id - b.id);

const analysis = {
  mode,
  repoRoot,
  workbookPath,
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
  sheet.getRange(`A${row}:${lastColumn}${row}`).copyFrom(sheet.getRange(`A${originalLastRow}:${lastColumn}${originalLastRow}`), "all");
}
const newRows = additions.map((item) => target === "shop" ? [null,item.id,item.skinId,null,null,null] : [
  null,
  item.id,
  item.skinName,
  null,
  item.skinPart,
  item.resourceKey,
  item.resourceKey,
  null,
  null,
  null,
  null,
  null,
  null,
]);
sheet.getRange(`A${firstNewRow}:${lastColumn}${lastNewRow}`).values = newRows;
sheet.getRange(`B${firstNewRow}:C${lastNewRow}`).format.fill = "#FFFF00";
if (target === "skin") sheet.getRange(`E${firstNewRow}:G${lastNewRow}`).format.fill = "#FFFF00";
if (JSON.stringify(sheet.getRange(originalAddress).values) !== originalValuesJson) {
  throw new Error("Original workbook values changed before export.");
}

await fs.mkdir(path.dirname(outputPath), { recursive: true });
await fs.mkdir(path.dirname(previewPath), { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
const rawExport = readRaw(outputPath);
for (const [name, cells] of Object.entries(rawOriginal)) {
  for (const [address, cell] of Object.entries(cells)) {
    const actual = rawExport[name]?.[address] ?? {value:null,kind:'blank',formula:null};
    if (JSON.stringify(actual) !== JSON.stringify(cell)) throw new Error(`OOXML original cell changed: ${name}!${address}`);
  }
}

const checkInput = await FileBlob.load(outputPath);
const checkBook = await SpreadsheetFile.importXlsx(checkInput);
const checkSheet = checkBook.worksheets.getItem(sheet.name);
for (const original of originals) {
  const range = checkBook.worksheets.getItem(original.name).getRange(original.address);
  if (JSON.stringify(range.values) !== original.values || JSON.stringify(range.formulas) !== original.formulas) throw new Error(`Original sheet changed: ${original.name}`);
}
if (JSON.stringify(checkSheet.getRange(originalAddress).values) !== originalValuesJson) {
  throw new Error("Export changed original workbook values.");
}
const expectedRows = additions.map((item) => target === "shop" ? [null,item.id,item.skinId,null,null,null] : [
  null,
  item.id,
  item.skinName,
  null,
  item.skinPart,
  item.resourceKey,
  item.resourceKey,
  null,
  null,
  null,
  null,
  null,
  null,
]);
if (JSON.stringify(checkSheet.getRange(`A${firstNewRow}:${lastColumn}${lastNewRow}`).values) !== JSON.stringify(expectedRows)) {
  throw new Error("Exported additions differ from the planned rows.");
}
const errors = await checkBook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 100 },
  summary: "Skin config formula error scan",
});
if (!errors.ndjson.includes("matched 0 entries")) throw new Error(`Formula errors found: ${errors.ndjson}`);

const preview = await checkBook.render({
  sheetName: checkSheet.name,
  range: `A${Math.max(1,firstNewRow-2)}:${lastColumn}${lastNewRow}`,
  scale: 1.5,
  format: "png",
});
await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));

console.log(JSON.stringify({
  ...analysis,
  outputPath,
  previewPath,
  newRange: `A${firstNewRow}:${lastColumn}${lastNewRow}`,
  originalValuesPreserved: true,
  formulaErrors: 0,
}, null, 2));
