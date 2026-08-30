# Move Modules Under `src/Modules/` Directory

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize the `src/` directory so module projects sit under a `Modules/` subdirectory, grouping them logically while keeping SharedKernel and Api at the top level.

**Architecture:** Move three module projects (`Modules.Catalog`, `Modules.Identity`, `Modules.Negotiations`) from `src/` into `src/Modules/`. Update all project references, solution file entries, and architecture tests to reflect the new paths.

**Tech Stack:** .NET 10, .slnx solution format, ArchUnitNET

## Global Constraints

- Assembly names must NOT change (only folder paths change)
- `InternalsVisibleTo` entries are assembly-name-based, not path-based — no changes needed there
- Test projects stay under `tests/` (unchanged)
- SharedKernel and Api stay at `src/` top level (unchanged)

## File Structure

### Files to Move (rename paths, not assembly names)

| Current Path | New Path |
|--------------|----------|
| `src/PriceNegotiationApp.Modules.Catalog/` | `src/Modules/PriceNegotiationApp.Modules.Catalog/` |
| `src/PriceNegotiationApp.Modules.Identity/` | `src/Modules/PriceNegotiationApp.Modules.Identity/` |
| `src/PriceNegotiationApp.Modules.Negotiations/` | `src/Modules/PriceNegotiationApp.Modules.Negotiations/` |

### Files to Modify

| File | Change |
|------|--------|
| `PriceNegotiationApp.slnx` | Update project paths for all 3 modules |
| `src/Modules/PriceNegotiationApp.Modules.Catalog/*.csproj` | Update `ProjectReference` to SharedKernel |
| `src/Modules/PriceNegotiationApp.Modules.Identity/*.csproj` | Update `ProjectReference` to SharedKernel |
| `src/Modules/PriceNegotiationApp.Modules.Negotiations/*.csproj` | Update `ProjectReference` to SharedKernel and Catalog |
| `src/PriceNegotiationApp.Api/*.csproj` | Update `ProjectReference` to all 3 modules |
| `tests/PriceNegotiationApp.ArchitectureTests/*.csproj` | Update `ProjectReference` to all 3 modules |
| `tests/PriceNegotiationApp.Modules.Catalog.Tests/*.csproj` | Update `ProjectReference` to Catalog |
| `tests/PriceNegotiationApp.Modules.Identity.Tests/*.csproj` | Update `ProjectReference` to Identity |
| `tests/PriceNegotiationApp.Modules.Negotiations.Tests/*.csproj` | Update `ProjectReference` to Negotiations |
| `tests/PriceNegotiationApp.IntegrationTests/*.csproj` | Update `ProjectReference` to all modules |
| `tests/PriceNegotiationApp.TestKit/*.csproj` | Update `ProjectReference` to modules if present |

---

### Task 1: Move module directories under `src/Modules/`

**Files:**
- Move: `src/PriceNegotiationApp.Modules.Catalog/` → `src/Modules/PriceNegotiationApp.Modules.Catalog/`
- Move: `src/PriceNegotiationApp.Modules.Identity/` → `src/Modules/PriceNegotiationApp.Modules.Identity/`
- Move: `src/PriceNegotiationApp.Modules.Negotiations/` → `src/Modules/PriceNegotiationApp.Modules.Negotiations/`

**Steps:**

- [ ] **Step 1: Create `src/Modules/` directory**

```bash
mkdir -p src/Modules
```

- [ ] **Step 2: Move Catalog module**

```bash
git mv src/PriceNegotiationApp.Modules.Catalog src/Modules/PriceNegotiationApp.Modules.Catalog
```

- [ ] **Step 3: Move Identity module**

```bash
git mv src/PriceNegotiationApp.Modules.Identity src/Modules/PriceNegotiationApp.Modules.Identity
```

- [ ] **Step 4: Move Negotiations module**

```bash
git mv src/PriceNegotiationApp.Modules.Negotiations src/Modules/PriceNegotiationApp.Modules.Negotiations
```

- [ ] **Step 5: Verify directories moved correctly**

```bash
ls src/Modules/
```

Expected output: `PriceNegotiationApp.Modules.Catalog`, `PriceNegotiationApp.Modules.Identity`, `PriceNegotiationApp.Modules.Negotiations`

- [ ] **Step 6: Commit directory move**

```bash
git add src/Modules/
git commit -m "Move module projects under src/Modules/ directory"
```

---

### Task 2: Update module .csproj ProjectReference paths

**Files:**
- Modify: `src/Modules/PriceNegotiationApp.Modules.Catalog/PriceNegotiationApp.Modules.Catalog.csproj`
- Modify: `src/Modules/PriceNegotiationApp.Modules.Identity/PriceNegotiationApp.Modules.Identity.csproj`
- Modify: `src/Modules/PriceNegotiationApp.Modules.Negotiations/PriceNegotiationApp.Modules.Negotiations.csproj`

**Steps:**

- [ ] **Step 1: Update Catalog csproj**

Update `ProjectReference` from:
```xml
<ProjectReference Include="..\..\src\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
```
To:
```xml
<ProjectReference Include="..\..\src\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
```

Wait — the relative path from `src/Modules/PriceNegotiationApp.Modules.Catalog/` to `src/PriceNegotiationApp.SharedKernel/` is `..\..\src\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj` (two directories up from Modules, then into src). This is correct as-is because the old path was `..\..\src\` from `src/PriceNegotiationApp.Modules.Catalog/` which was actually `..\PriceNegotiationApp.SharedKernel\`. Let me verify:

Old path: `src/PriceNegotiationApp.Modules.Catalog/` → `..\PriceNegotiationApp.SharedKernel\...` (one directory up to `src/`, then into SharedKernel)

New path: `src/Modules/PriceNegotiationApp.Modules.Catalog/` → `..\..\PriceNegotiationApp.SharedKernel\...` (two directories up to `src/`, then into SharedKernel)

So the path changes from `..` to `..\..` for SharedKernel references.

Update Catalog csproj `ProjectReference` to:
```xml
<ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
```

- [ ] **Step 2: Update Identity csproj**

Update `ProjectReference` from:
```xml
<ProjectReference Include="..\..\src\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
```
To:
```xml
<ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
```

- [ ] **Step 3: Update Negotiations csproj**

Update `ProjectReference` to SharedKernel from:
```xml
<ProjectReference Include="..\..\src\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
```
To:
```xml
<ProjectReference Include="..\..\PriceNegotiationApp.SharedKernel\PriceNegotiationApp.SharedKernel.csproj" />
```

Update `ProjectReference` to Catalog from:
```xml
<ProjectReference Include="..\..\src\PriceNegotiationApp.Modules.Catalog\PriceNegotiationApp.Modules.Catalog.csproj" />
```
To:
```xml
<ProjectReference Include="..\..\Modules\PriceNegotiationApp.Modules.Catalog\PriceNegotiationApp.Modules.Catalog.csproj" />
```

- [ ] **Step 4: Build to verify module csproj changes**

```bash
dotnet build --no-restore
```

- [ ] **Step 5: Commit csproj updates**

```bash
git add src/Modules/
git commit -m "Update module ProjectReference paths for new directory structure"
```

---

### Task 3: Update Api project .csproj references

**Files:**
- Modify: `src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj`

**Steps:**

- [ ] **Step 1: Read current Api csproj**

Read `src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj` to see current `ProjectReference` entries.

- [ ] **Step 2: Update Api csproj ProjectReferences**

Update all module `ProjectReference` paths from:
```xml
<ProjectReference Include="..\src\PriceNegotiationApp.Modules.Catalog\PriceNegotiationApp.Modules.Catalog.csproj" />
<ProjectReference Include="..\src\PriceNegotiationApp.Modules.Identity\PriceNegotiationApp.Modules.Identity.csproj" />
<ProjectReference Include="..\src\PriceNegotiationApp.Modules.Negotiations\PriceNegotiationApp.Modules.Negotiations.csproj" />
```
To (paths vary based on current structure — adjust relative paths):
```xml
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Catalog\PriceNegotiationApp.Modules.Catalog.csproj" />
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Identity\PriceNegotiationApp.Modules.Identity.csproj" />
<ProjectReference Include="..\Modules\PriceNegotiationApp.Modules.Negotiations\PriceNegotiationApp.Modules.Negotiations.csproj" />
```

- [ ] **Step 3: Build to verify Api references**

```bash
dotnet build --no-restore
```

- [ ] **Step 4: Commit Api csproj update**

```bash
git add src/PriceNegotiationApp.Api/
git commit -m "Update Api ProjectReference paths for module directory move"
```

---

### Task 4: Update test project .csproj references

**Files:**
- Modify: `tests/PriceNegotiationApp.ArchitectureTests/*.csproj`
- Modify: `tests/PriceNegotiationApp.Modules.Catalog.Tests/*.csproj`
- Modify: `tests/PriceNegotiationApp.Modules.Identity.Tests/*.csproj`
- Modify: `tests/PriceNegotiationApp.Modules.Negotiations.Tests/*.csproj`
- Modify: `tests/PriceNegotiationApp.IntegrationTests/*.csproj`
- Modify: `tests/PriceNegotiationApp.TestKit/*.csproj`

**Steps:**

- [ ] **Step 1: Read all test csproj files**

Read each test project's `.csproj` to see current `ProjectReference` entries.

- [ ] **Step 2: Update test csproj references**

For each test project, update `ProjectReference` paths to reflect the new module locations under `src/Modules/`.

Example for Catalog Tests (adjust paths based on current structure):
```xml
<!-- Old -->
<ProjectReference Include="..\..\src\PriceNegotiationApp.Modules.Catalog\PriceNegotiationApp.Modules.Catalog.csproj" />
<!-- New -->
<ProjectReference Include="..\..\src\Modules\PriceNegotiationApp.Modules.Catalog\PriceNegotiationApp.Modules.Catalog.csproj" />
```

- [ ] **Step 3: Build entire solution**

```bash
dotnet build
```

- [ ] **Step 4: Commit test csproj updates**

```bash
git add tests/
git commit -m "Update test ProjectReference paths for module directory move"
```

---

### Task 5: Update solution file

**Files:**
- Modify: `PriceNegotiationApp.slnx`

**Steps:**

- [ ] **Step 1: Read current solution file**

Read `PriceNegotiationApp.slnx` to see current project paths.

- [ ] **Step 2: Update module project paths**

Update the `ProjectPath` attributes for all 3 module projects from:
```xml
<Project Path="src\PriceNegotiationApp.Modules.Catalog\PriceNegotiationApp.Modules.Catalog.csproj" />
<Project Path="src\PriceNegotiationApp.Modules.Identity\PriceNegotiationApp.Modules.Identity.csproj" />
<Project Path="src\PriceNegotiationApp.Modules.Negotiations\PriceNegotiationApp.Modules.Negotiations.csproj" />
```
To:
```xml
<Project Path="src\Modules\PriceNegotiationApp.Modules.Catalog\PriceNegotiationApp.Modules.Catalog.csproj" />
<Project Path="src\Modules\PriceNegotiationApp.Modules.Identity\PriceNegotiationApp.Modules.Identity.csproj" />
<Project Path="src\Modules\PriceNegotiationApp.Modules.Negotiations\PriceNegotiationApp.Modules.Negotiations.csproj" />
```

- [ ] **Step 3: Verify solution loads correctly**

```bash
dotnet build
```

- [ ] **Step 4: Commit solution file update**

```bash
git add PriceNegotiationApp.slnx
git commit -m "Update solution file paths for module directory move"
```

---

### Task 6: Run full validation

**Files:** None (verification only)

**Steps:**

- [ ] **Step 1: Clean and rebuild**

```bash
dotnet clean
dotnet build
```

- [ ] **Step 2: Run all tests**

```bash
dotnet test
```

Expected: All 124 tests pass.

- [ ] **Step 3: Verify directory structure**

```bash
ls -la src/
ls -la src/Modules/
```

Expected:
```
src/
├── PriceNegotiationApp.Api/
├── PriceNegotiationApp.SharedKernel/
└── Modules/
    ├── PriceNegotiationApp.Modules.Catalog/
    ├── PriceNegotiationApp.Modules.Identity/
    └── PriceNegotiationApp.Modules.Negotiations/
```

- [ ] **Step 4: Final commit (if any fixes needed)**

```bash
git add -A
git commit -m "Fix any issues from directory reorganization"
```
