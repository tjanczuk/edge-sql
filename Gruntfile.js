'use strict';
/*globals initConfig, appPath */
/*jshint camelcase: false */

module.exports = function (grunt) {

    // Load grunt tasks automatically
    require('load-grunt-tasks')(grunt);
    // Time how long tasks take. Can help when optimizing build times
    require('time-grunt')(grunt);

    grunt.initConfig(
        {
            pkg: grunt.file.readJSON('package.json'),

            yuidoc: {
                compile: {
                    linkNatives: "true",
                    name: '<%= pkg.name %>',
                    description: '<%= pkg.description %>',
                    version: '<%= pkg.version %>',
                    url: '<%= pkg.homepage %>',
                    options: {
                        paths: ['./lib'],
                        outdir: 'doc'
                    }
                }
            },

            watch: {
                files: ['src/edge-sql.js'],
                tasks: ['karma:unit:run'],
                options: {
                    livereload: true
                }
            },

            jasmine_node: {
                all: [],
                options: {
                    coffee: false,
                    verbose: true,
                    match: '.',
                    matchall: false,
                    specFolders: ['./test/spec/'],
                    projectRoot: '',
                    forceExit: false,

                    jUnit: {
                        report: true,
                        savePath: "./build/reports/jasmine/",
                        useDotNotation: false,
                        consolidate: true
                    }
                },
                single: {
                    options: {
                        specFolders: ['./test/spec/'],
                        useDotNotation: false,
                        autotest: false
                    }
                },
                auto: {
                    options: {
                        autotest: true,
                        forceExit: false
                    }
                }

            }
        }
    );

    grunt.registerTask('test', ['jasmine_node:single']);
    grunt.registerTask('default', ['test']);
};
