namespace Lunet.Core;

/// <summary>Workflow installed only after consent to private cloud compilation.</summary>
public static class GitHubGameBuildWorkflow
{
    /// <summary>Official Lunet project APK workflow for a private GitHub repository.</summary>
    public const string Source = """
name: Lunet Game APK
run-name: Lunet __DOLLAR__{{ inputs.build_id }}
on:
  workflow_dispatch:
    inputs:
      build_id:
        required: true
        type: string
      game_path:
        required: true
        type: string
      package_id:
        required: true
        type: string
      game_title:
        required: true
        type: string
permissions:
  contents: read
jobs:
  build:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4
        with:
          path: input
      - uses: actions/checkout@v4
        with:
          repository: AbnerCruz/Ecosystem
          ref: main
          path: ecosystem
          persist-credentials: false
      - name: Prepare project snapshot
        env:
          GAME_PATH: __DOLLAR__{{ inputs.game_path }}
        run: |
          python3 - <<'PY'
          import os, pathlib, shutil, zipfile
          source = pathlib.Path("input") / os.environ["GAME_PATH"]
          target = pathlib.Path("game")
          target.mkdir()
          with zipfile.ZipFile(source) as archive:
              entries = archive.infolist()
              if len(entries) > 10000:
                  raise ValueError("Too many project files")
              roots = {p.filename.split("/")[0] for p in entries if p.filename}
              if len(roots) != 1:
                  raise ValueError("Project ZIP must have one root folder")
              total = 0
              for item in entries:
                  parts = pathlib.PurePosixPath(item.filename).parts
                  if len(parts) < 2 or item.is_dir():
                      continue
                  if any(part in ("..", ".", "") for part in parts):
                      raise ValueError("Unsafe ZIP entry")
                  if (item.external_attr >> 16) & 0o170000 == 0o120000:
                      raise ValueError("Symbolic link not allowed")
                  total += item.file_size
                  if total > 512 * 1024 * 1024:
                      raise ValueError("Project too large")
                  dest = target.joinpath(*parts[1:])
                  dest.parent.mkdir(parents=True, exist_ok=True)
                  with archive.open(item) as src, open(dest, "wb") as dst:
                      shutil.copyfileobj(src, dst)
          if not (target / "lunet.json").is_file():
              raise ValueError("lunet.json missing")
          PY
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - name: Compile game C#
        run: |
          cd ecosystem/apps/lunet2d
          dotnet build runtime-template/Lunet.GameCode.csproj -c Release \
            -p:GameSourceRoot="$GITHUB_WORKSPACE/game"
      - name: Install Android workload
        run: dotnet workload install android
      - name: Generate game APK
        env:
          GAME_PACKAGE: __DOLLAR__{{ inputs.package_id }}
          GAME_TITLE: __DOLLAR__{{ inputs.game_title }}
        run: |
          cd ecosystem/apps/lunet2d
          dotnet build runtime-template/Lunet.Game.Android.csproj -c Release \
            -p:GameSourceRoot="$GITHUB_WORKSPACE/game" \
            -p:ApplicationId="$GAME_PACKAGE" \
            -p:ApplicationTitle="$GAME_TITLE" \
            -p:AndroidSdkDirectory="$ANDROID_HOME"
      - name: Collect APK
        run: |
          apk=$(find ecosystem/apps/lunet2d/runtime-template/bin -name '*-Signed.apk' | head -n1)
          test -n "$apk"
          unzip -l "$apk" | grep -q 'assets/game/Game.dll'
          mkdir dist
          cp "$apk" dist/game.apk
          sha256sum dist/game.apk > dist/SHA256SUMS.txt
      - uses: actions/upload-artifact@v4
        with:
          name: lunet-game-apk
          path: dist/*
          if-no-files-found: error
""".Replace("__DOLLAR__", "$", System.StringComparison.Ordinal);
}
