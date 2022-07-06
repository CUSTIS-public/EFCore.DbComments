EFCore.DbComment

[![latest version](https://img.shields.io/nuget/v/CUSTIS.EFCore.DbComments)](https://www.nuget.org/packages/CUSTIS.EFCore.DbComments/)
[![downloads](https://img.shields.io/nuget/dt/CUSTIS.EFCore.DbComments)](https://www.nuget.org/packages/CUSTIS.EFCore.DbComments/)

Add comment for objects (table, column) from code to database.
You can use it with xml comment summary or Description clr attr.

# Using comments from XmlDoc

```csharp
/// <summary>User</summary>
public class User
{
    /// <summary>Full name</summary>
    public string Name { get; set; }
}
```

In DbContext.OnModelCreating(ModelBuilder builder) insert
```csharp
modelBuilder.CommentModelFromXml();
```

And then you can do ```dotnet ef migrations add ...```

Don't forget to [enable](https://docs.microsoft.com/ru-ru/dotnet/csharp/codedoc) XML documentation

# Using comments from [Description] attr

```csharp
[Description("User")]
public class User
{
    [Description("Full name")]
    public string Name { get; set; }
}
```

In DbContext.OnModelCreating(ModelBuilder builder) insert
```csharp
modelBuilder.CommentModelFromDescriptionAttr();
```

# XmlDoc from nuget packages
If you inherit entities from classes from nuget packages, you should also enable packaging xml documentation in packages and enable copying their documentation to output directory.

## Coping XmlDoc from nuget package to output directory
To copy XmlDocs, you need to add section to your startup project ```.csproj```  or ```Directory.Build.props```:
 ```xml
    <Target Name="_ResolveCopyLocalNuGetPackagePdbsAndXml" Condition="$(CopyLocalLockFileAssemblies) == true" AfterTargets="ResolveReferences">
        <ItemGroup>
            <ReferenceCopyLocalPaths Include="@(ReferenceCopyLocalPaths->'%(RootDir)%(Directory)%(Filename).xml')" Condition="'%(ReferenceCopyLocalPaths.NuGetPackageId)' != '' and Exists('%(RootDir)%(Directory)%(Filename).xml')" />
        </ItemGroup>
    </Target>
 ```
 
# Links
* Nuget - https://www.nuget.org/packages/CUSTIS.EFCore.DbComments/
