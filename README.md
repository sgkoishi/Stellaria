# Stellaria
A multi-world plugin for TShock. It forward all packets from some players to `game server`.  
Use `/sv [name]` to switch to different world.  
Permission for using `/sv` is `chireiden.stellaria.use`. 

### Config File
By default, a config file will be created.  
* "Host": true if it is host server.  
* "Key": A private key to verify server.
  * Key can be same for different server.
  * If Key in Host's `stellatia.json -> Servers` does not match room server's Key, players still can join but room server can't get the real IP address of player.  
* "Name": Displayed name. Player use this name to join, so don't be too complex.  
  * **There must be a server in `Servers` have same `Name` as current server's Name.**   
  * Name in `Servers` should be unique.  
* "JoinBytes": These bytes contain version information of Terraria. *Don't change it unless game update or modified client.*  
* "Servers": A list of all server it can forward. Contains itself.  
* "Permission": Permission required to join to this world.  
* "OnEnter": Not implemented yet.  
* "OnLeave": Not implemented yet.  
* "GlobalCommands": These commands will be handled by host server, even if they are forwarded.

#### Sample config
Server 7777 (Wrapper):

    {
      "Host": true,
      "Key": "kisvK7HS+svZVdlzan4RZ072OdC1gNpIoOy56Uao6ZU=", // Key 1, random generated
      "Name": "wrapper", // Name 1
      "JoinBytes": "AQtUZXJyYXJpYTE5NA==",
      "Servers": [
        {
          "Address": "127.0.0.1",
          "Port": 7776,
          "Name": "wrapper",
          "Permission": "",
          "OnEnter": [],
          "OnLeave": [],
          "GlobalCommands": [
            "sv",
            "who"
          ],
          "SpawnX": 1000,
          "SpawnY": 300,
          "Key": "kisvK7HS+svZVdlzan4RZ072OdC1gNpIoOy56Uao6ZU="
        },
        {
          "Address": "127.0.0.1",
          "Port": 7777,
          "Name": "lobby",
          "Permission": "",
          "OnEnter": [],
          "OnLeave": [],
          "GlobalCommands": [
            "sv",
            "who"
          ],
          "SpawnX": 1000,
          "SpawnY": 300,
          "Key": "aAdgfl52k8OamHRtrWsvbhJMXlcT6dhF9PuLur91mEA="
        },
        {
          "Address": "127.0.0.1",
          "Port": 7778,
          "Name": "game1",
          "Permission": "",
          "OnEnter": [],
          "OnLeave": [],
          "GlobalCommands": [
            "sv",
            "who"
          ],
          "SpawnX": 1000,
          "SpawnY": 300,
          "Key": "ADNzptEEsyuuZZxRWPPUawPKi2rJIUU3ahv7n107DuE="
        },
        {
          "Address": "127.0.0.1",
          "Port": 7779,
          "Name": "game2",
          "Permission": "",
          "OnEnter": [],
          "OnLeave": [],
          "GlobalCommands": [
            "sv",
            "who"
          ],
          "SpawnX": 1000,
          "SpawnY": 300,
          "Key": "LJ7zd/hZ3WpaKloWEYRS3dsIl2F99wNNoFkJQ8leKCg="
        }
      ]
    }

Server 7777 (Lobby):

    {
      "Host": false,
      "Key": "aAdgfl52k8OamHRtrWsvbhJMXlcT6dhF9PuLur91mEA=",
      "Name": "lobby",
      "JoinBytes": "AQtUZXJyYXJpYTE5NA==",
      "Servers": []
    }

Server 7778 (Game Server 1):

    {
      "Host": false,
      "Key": "ADNzptEEsyuuZZxRWPPUawPKi2rJIUU3ahv7n107DuE=",
      "Name": "game1",
      "JoinBytes": "AQtUZXJyYXJpYTE5NA==",
      "Servers": []
    }

Server 7779 (Game Server 2):

    {
      "Host": false,
      "Key": "LJ7zd/hZ3WpaKloWEYRS3dsIl2F99wNNoFkJQ8leKCg=",
      "Name": "game2",
      "JoinBytes": "AQtUZXJyYXJpYTE5NA==",
      "Servers": []
    }