@echo begin generate proto....

cd db

..\protoc.exe --proto_path=./ --csharp_out=./ db_player.proto

cd..

@echo finish generate db proto..
pause