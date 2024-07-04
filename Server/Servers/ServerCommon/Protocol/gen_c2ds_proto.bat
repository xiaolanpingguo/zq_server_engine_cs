@echo begin generate proto....

cd c2ds

..\protoc.exe --proto_path=./ --csharp_out=./ c2ds_msg.proto

cd..

@echo finish generate c2ds proto..
pause