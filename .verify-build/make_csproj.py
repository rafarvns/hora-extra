import re
import sys

BS = chr(92)
src = sys.argv[1]
dst = sys.argv[2]

s = open(src, encoding='utf-8-sig').read()
s = re.sub(r'\s*<Compile Include="[^"]*" />', '', s)

glob = BS.join(['..', 'hora-extra-client', 'Assets', 'Scripts', '**', '*.cs'])
s = s.replace('<ItemGroup>\n    <Analyzer',
              '<ItemGroup>\n    <Compile Include="' + glob + '" />\n  </ItemGroup>\n  <ItemGroup>\n    <Analyzer', 1)

s = re.sub(r'<OutputPath>[^<]*</OutputPath>',
           '<OutputPath>bin' + BS + '</OutputPath><IntermediateOutputPath>obj' + BS + '</IntermediateOutputPath>', s)

def fix_hint(m):
    path = m.group(1)
    if re.match(r'^[A-Za-z]:', path):
        return m.group(0)
    return '<HintPath>..' + BS + 'hora-extra-client' + BS + path + '</HintPath>'

s = re.sub(r'<HintPath>([^<]*)</HintPath>', fix_hint, s)
open(dst, 'w', encoding='utf-8').write(s)
print('compile items:', s.count('Compile Include'))
