"""只读解析 XLSX 实际单元格，独立于 artifact-tool 检查共享字符串空值。"""
import json
import posixpath
import sys
import xml.etree.ElementTree as ET
import zipfile

NS = {'m': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
with zipfile.ZipFile(sys.argv[1]) as archive:
    strings = []
    if 'xl/sharedStrings.xml' in archive.namelist():
        for item in ET.fromstring(archive.read('xl/sharedStrings.xml')):
            strings.append(''.join(t.text or '' for t in item.findall('.//m:t', NS)))
    rels = {r.get('Id'): r.get('Target') for r in ET.fromstring(archive.read('xl/_rels/workbook.xml.rels'))}
    result = {}
    for sheet in ET.fromstring(archive.read('xl/workbook.xml')).findall('m:sheets/m:sheet', NS):
        target = rels[sheet.get('{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id')]
        target = target.lstrip('/') if target.startswith('/') else posixpath.normpath('xl/' + target)
        cells = {}
        for cell in ET.fromstring(archive.read(target)).findall('.//m:sheetData/m:row/m:c', NS):
            value = cell.find('m:v', NS)
            value = value.text if value is not None else None
            kind = cell.get('t', 'n')
            if kind == 's':
                value = strings[int(value)]
            elif kind == 'inlineStr':
                value = ''.join(t.text or '' for t in cell.findall('.//m:t', NS))
            formula = cell.find('m:f', NS)
            # 空字符串和空单元格在 Luban 中均为空；保留其它类型与公式。
            cells[cell.get('r')] = {'value': value or None, 'kind': 'blank' if not value else ('text' if kind in ('s', 'inlineStr', 'str') else kind), 'formula': formula.text if formula is not None else None}
        result[sheet.get('name')] = cells
    print(json.dumps(result, ensure_ascii=True))
