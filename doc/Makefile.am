if ENABLE_MONODOC
TARGETS = muine-docs.zip muine-docs.tree muine-docs.source

install-data-local:
	install -m 644 $(srcdir)/muine-docs.source `monodoc --get-sourcesdir`
	install -m 644 $(srcdir)/muine-docs.tree `monodoc --get-sourcesdir`
	install -m 644 $(srcdir)/muine-docs.zip `monodoc --get-sourcesdir`
else
TARGETS = 
endif

noinst_DATA = $(TARGETS)

ASSEMBLER = $(MONODOC) --assemble
UPDATER = $(MONODOC) --update

muine-docs.zip muine-docs.tree: $(srcdir)/en/*/*.xml $(srcdir)/en/*.xml
	$(ASSEMBLER) --ecma $(srcdir)/en -o muine-docs

update:
	$(UPDATER) $(top_builddir)/PluginLib/muine-plugin.dll -o ./en -f
	$(UPDATER) $(top_builddir)/ClientLib/muine-dbus.dll -o ./en -f

CLEANFILES = muine-docs.zip muine-docs.tree

EXTRA_DIST = muine-docs.source
if ENABLE_MONODOC
EXTRA_DIST += muine-docs.tree muine-docs.zip
endif

dist-hook:
	mkdir -p $(distdir)/en
	cp $(srcdir)/en/*.xml $(distdir)/en/
	mkdir -p $(distdir)/en/MuineDBusLib
	cp $(srcdir)/en/MuineDBusLib/*.xml $(distdir)/en/MuineDBusLib/
	mkdir -p $(distdir)/en/MuinePluginLib
	cp $(srcdir)/en/MuinePluginLib/*.xml $(distdir)/en/MuinePluginLib/