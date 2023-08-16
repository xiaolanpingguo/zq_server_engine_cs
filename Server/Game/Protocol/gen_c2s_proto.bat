@echo begin generate proto....

cd c2s

..\protoc.exe --proto_path=./ --csharp_out=./ c2s_common.proto
..\protoc.exe --proto_path=./ --csharp_out=./ c2s_login.proto
..\protoc.exe --proto_path=./ --csharp_out=./ c2s_baseinfo.proto

cd..

@echo finish generate c2s proto..
pause