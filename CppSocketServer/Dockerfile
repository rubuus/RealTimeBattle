FROM mcr.microsoft.com/windows/servercore:ltsc2022

WORKDIR /app
COPY x64/Release/CppSocketServer.exe .

EXPOSE 7777

CMD ["CppSocketServer.exe"]