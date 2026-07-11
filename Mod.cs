name: Compile BONELAB Mod
on: [push]
jobs:
  build:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v4

    - name: Setup MSBuild
      uses: microsoft/setup-msbuild@v2

    - name: Fetch Official Modding Dependencies
      run: |
        mkdir refs
        curl -L -o refs/MelonLoader.zip https://github.com
        powershell Expand-Archive refs/MelonLoader.zip -DestinationPath refs/temp
        move refs\temp\MelonLoader\MelonLoader.dll refs\MelonLoader.dll
        
        curl -L -o refs/BoneLib.zip https://github.com
        powershell Expand-Archive refs/BoneLib.zip -DestinationPath refs\temp2
        move refs\temp2\Mods\BoneLib.dll refs\Il2CppSLZ.Marrow.dll
        
        curl -L -o refs/UnityRefs.zip https://github.com
        powershell Expand-Archive refs/UnityRefs.zip -DestinationPath refs\temp3
        
        echo . > refs/UnityEngine.CoreModule.dll
      shell: cmd

    - name: Compile Mod Solution
      run: msbuild Project.csproj /p:Configuration=Release /p:OutputPath=./build
      shell: cmd

    - name: Upload Finished Mod
      uses: actions/upload-artifact@v4
      with:
        name: Compiled-Mod
        path: ./build/
