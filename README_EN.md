# RimTalk StyleExpand

English | [中文](README.md)

**Make RimTalk dialogues imitate specified writing styles**

A style expansion Mod based on vector retrieval, supporting custom styles.

## Features

- **Style Management**: One txt file = one style
- **Vector Retrieval**: Automatically retrieve similar style fragments based on character traits
- **Scriban Variables**: Support custom usage in RimTalk advanced templates
- **Caching**: Chunking results and embeddings cached locally
- **Localization**: Chinese / English
- **Multi-version**: RimWorld 1.5 / 1.6

## Quick Start

1. Place `.txt` style files in the `Styles/` directory
2. Launch game, open Mod settings
3. Click **Scan Styles** to scan files
4. Select a style
5. Click **Chunk Style** to process
6. Done!

### Style File Recommendations

| Language | Recommended Size | Notes |
|----------|------------------|-------|
| Chinese | 5,000 - 50,000 characters | Too few = poor retrieval, too many = long processing |
| English | 3,000 - 30,000 words | Same as above |

> Larger files can be used, but chunking and embedding calculation will take longer.

## Requirements

- **RimTalk** Mod (required)
- **Vector API** (local Ollama or cloud API)

### Supported APIs

- Ollama (default: `http://localhost:11434/api/embeddings`)
- OpenAI Embedding API
- Other OpenAI-compatible APIs

Recommended models: `bge-small-zh`, `text-embedding-3-small`

## Scriban Variables

Use in RimTalk advanced mode:

| Variable | Description |
|----------|-------------|
| `{{style_name}}` | Current style name |
| `{{style_base_prompt}}` | Base instruction prompt |
| `{{style_prompt}}` | Style description |
| `{{style_chunks}}` | Retrieved example chunks |
| `{{style_full}}` | Complete style prompt (all above combined) |

### Example Template

```
[Style Instruction]
{{style_base_prompt}}

{{style_prompt}}

{{style_chunks}}
```

## File Structure

```
StyleExpand/
├── About/About.xml
├── Languages/
│   ├── English/Keyed/StyleExpand.xml
│   └── ChineseSimplified/Keyed/StyleExpand.xml
├── 1.5/Assemblies/
│   ├── RimTalk-StyleExpand.dll
│   └── Newtonsoft.Json.dll
├── 1.6/Assemblies/
│   ├── RimTalk-StyleExpand.dll
│   └── Newtonsoft.Json.dll
├── Styles/
│   ├── Tsundere.txt
│   ├── Classical.txt
│   ├── Satirical.txt
│   └── .cache/           # Auto-generated cache
└── Source/
```

## Progress

### v1.0 (Current)

- [x] Vector retrieval
- [x] Prompt injection
- [x] Style management (single selection)
- [x] Configuration UI
- [x] Text chunking (paragraph + sentence)
- [x] Embedding cache
- [x] Manual reload button
- [x] Open folder button
- [x] Scriban variable registration
- [x] Editable base prompt
- [x] Help window
- [x] Status/error messages
- [x] Reset buttons
- [x] Chinese/English localization
- [x] Chunking progress display
- [x] Large file warning
- [x] RimWorld 1.5 / 1.6 support

### v1.1 (Planned)

- [ ] Auto-reload on file changes
- [ ] Style preview
- [ ] More preset styles
- [ ] Chunking algorithm optimization
- [ ] Batch chunking button
- [ ] Large file sampling strategy
- [ ] Batch chunking + resume from interruption
- [ ] LLM auto-generate style prompt
  - Support reusing RimTalk API config
  - Support separate API config for generation

### v1.2 (Future)

- [ ] Iterate based on community feedback

## Build

```bash
# Build for 1.5
dotnet build -p:GameVersion=1.5

# Build for 1.6
dotnet build -p:GameVersion=1.6
```

## Dependencies

- Harmony 2.x
- RimTalk Mod
- Newtonsoft.Json

## License

Based on RimTalk, follows the same license.

## Author

NUOQI_P