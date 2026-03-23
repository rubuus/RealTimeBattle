FROM mcr.microsoft.com/windows/servercore:ltsc2019

WORKDIR /app
COPY x64/Debug/CppSocketServer.exe .

EXPOSE 7777

CMD ["CppSocketServer.exe"]