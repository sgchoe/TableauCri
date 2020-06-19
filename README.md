# TableauCri
Manage/migrate Tableau objects using REST API

This C# .NET Core assembly contains a hodgepodge of helper classes that call a Tableau server's REST API.  It was developed as part of an effort to automate the migration of projects, groups, users, datasources, and workbooks from one Tableau server to another.  The migration scenario it served isn't likely to be repeated and it seemed a waste to just throw away the code so I'm placing it here in the hope that maybe someone will find some part of it useful.  Note that a lot of installation-specific assumptions exist and will need to be accounted for, such as an authN/authZ model that uses AD domain accounts with local Tableau groups, (not great) handing of whitespace in object names, etc.

Migration of workbooks that use published datasources is particularly fragile due to a seeming bug in the REST API that prevents datasources from being downloaded if they contain whitespace in the name.  A kludgy workaround is implemented here that involves using unofficial URLs to download datasources in bulk as well as a manually-driven way of passing credentials for (re-)publishing.

Migration of objects with duplicate names is also fairly fragile, mostly due to my own laziness.
