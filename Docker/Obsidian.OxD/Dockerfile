FROM microsoft/dotnet:2.2-sdk

RUN git clone https://github.com/obsidianproject/Obsidian-StratisNode.git \
    && cd /Obsidian-StratisNode/src/Obsidian.OxD \
	&& dotnet build
	
VOLUME /root/.stratisbitcoin

WORKDIR /Obsidian-StratisNode/src/Obsidian.OxD

COPY obsidian.conf.docker /root/.stratisnode/obsidianx/ObsidianXMain/obsidianx.conf

EXPOSE 46660 46661 47221 

CMD ["dotnet", "run"]
